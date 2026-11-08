using System.Globalization;
using Foresight.Api.Accounts;
using Foresight.Api.Data;
using Foresight.Billing;
using Foresight.Shared;
using Foresight.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Channels;

public enum LinkOutcome { Ready, NotEntitled, NotConfigured }

/// <summary>
/// The premium channel follows the subscriptions. Each subscriber gets a
/// join link of their own. The bot lets in an account that has premium,
/// and only the first Telegram user who comes with its link. Whoever has
/// premium no longer is removed on the next sync, and their link stops
/// working. The scheduler asks for a sync on every tick.
/// </summary>
public sealed class ChannelMembership
{
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly PremiumAccess _premium;
    private readonly IChannelMembers _bot;
    private readonly TelegramOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ChannelMembership> _logger;

    // One sync at a time: two would answer the same request twice.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChannelMembership(
        IDbContextFactory<AppDbContext> db,
        PremiumAccess premium,
        IChannelMembers bot,
        IOptions<TelegramOptions> options,
        TimeProvider clock,
        ILogger<ChannelMembership> logger)
    {
        _db = db;
        _premium = premium;
        _bot = bot;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    private bool IsConfigured =>
        _options.BotToken.Length > 0 && _options.PremiumChannel.Length > 0;

    /// <summary>The account's own link, made on its first ask.</summary>
    public async Task<(LinkOutcome Outcome, string? Link)> LinkForAsync(
        string email, CancellationToken ct = default)
    {
        if (!IsConfigured) return (LinkOutcome.NotConfigured, null);
        var owner = UserStore.Canonical(email);

        if (!await _premium.HasAsync(owner, ct))
            return (LinkOutcome.NotEntitled, null);

        await using var db = await _db.CreateDbContextAsync(ct);
        if (await db.ChannelLinks.FindAsync([owner], ct) is { } row)
            return (LinkOutcome.Ready, row.Link);

        // The name shows in the channel's list of links. A random tag, so
        // no email appears there.
        var link = await _bot.CreateJoinLinkAsync(
            "subscriber " + Guid.NewGuid().ToString("N")[..8], ct);
        db.ChannelLinks.Add(new ChannelLinkRow
        {
            Owner = owner, Link = link,
            CreatedUtc = _clock.GetUtcNow().UtcDateTime,
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return (LinkOutcome.Ready, link);
        }
        catch (DbUpdateException)
        {
            // A second ask made a link at the same moment and was saved
            // first. Keep that one; this one must not stay open.
            await _bot.RevokeLinkAsync(link, ct);
            await using var again = await _db.CreateDbContextAsync(ct);
            var saved = await again.ChannelLinks.FindAsync([owner], ct);
            return (LinkOutcome.Ready, saved!.Link);
        }
    }

    /// <summary>Answers the waiting join requests, then removes whoever
    /// has premium no longer.</summary>
    public async Task<ChannelSyncReport> SyncAsync(
        CancellationToken ct = default)
    {
        if (!IsConfigured) return new ChannelSyncReport(0, 0, 0);

        await _gate.WaitAsync(ct);
        try
        {
            var (approved, declined) = await AnswerAsync(ct);
            var removed = await RemoveLapsedAsync(ct);
            return new ChannelSyncReport(approved, declined, removed);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>For a deleted account: out of the channel now, if Telegram
    /// can be told. If not, the row stays and the next sync does it, since
    /// an account without a subscription has no premium.</summary>
    public async Task ForgetAsync(string email, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        await using var db = await _db.CreateDbContextAsync(ct);
        var row = await db.ChannelLinks.FindAsync(
            [UserStore.Canonical(email)], ct);
        if (row is not null)
            await TryRemoveAsync(db, row, ct);
    }

    private async Task<(int Approved, int Declined)> AnswerAsync(
        CancellationToken ct)
    {
        var (requests, last) = await _bot.ReadJoinRequestsAsync(ct);
        int approved = 0, declined = 0;
        await using var db = await _db.CreateDbContextAsync(ct);

        foreach (var request in requests.Where(IsPremiumChannel))
        {
            var row = request.Link is null ? null
                : await db.ChannelLinks.SingleOrDefaultAsync(
                    l => l.Link == request.Link, ct);
            // The link must be ours, its first visitor must be this one,
            // and its account must have premium now.
            var welcome = row is not null
                && (row.TelegramUserId is null
                    || row.TelegramUserId == request.UserId)
                && await _premium.HasAsync(row.Owner, ct);
            try
            {
                if (welcome)
                {
                    // Written down before the door opens: a member the
                    // database does not know could never be removed.
                    row!.TelegramUserId = request.UserId;
                    await db.SaveChangesAsync(ct);
                    await _bot.ApproveAsync(request.UserId, ct);
                    approved++;
                }
                else
                {
                    await _bot.DeclineAsync(request.UserId, ct);
                    declined++;
                }
            }
            catch (TelegramException ex)
            {
                // Withdrawn meanwhile, perhaps. The rest are answered.
                _logger.LogWarning("A join request was not answered: {Why}",
                    ex.Message);
            }
        }

        // Only now: a crash before this line means the same requests come
        // again on the next sync, and every answer is safe to repeat.
        if (last is { } id)
            await _bot.MarkReadAsync(id, ct);
        return (approved, declined);
    }

    private async Task<int> RemoveLapsedAsync(CancellationToken ct)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        var removed = 0;
        foreach (var row in await db.ChannelLinks.ToListAsync(ct))
        {
            // The rule of chapter 8, the one the website asks too. A
            // deleted account has no subscription left, so it is out.
            if (await _premium.HasAsync(row.Owner, ct)) continue;
            if (await TryRemoveAsync(db, row, ct)) removed++;
        }
        return removed;
    }

    // Out of the channel, the link dead, the row gone. If Telegram cannot
    // be told, the row stays, and the next sync tries again.
    private async Task<bool> TryRemoveAsync(
        AppDbContext db, ChannelLinkRow row, CancellationToken ct)
    {
        try
        {
            if (row.TelegramUserId is { } user)
                await _bot.RemoveAsync(user, ct);
            await _bot.RevokeLinkAsync(row.Link, ct);
        }
        catch (TelegramException ex)
        {
            _logger.LogWarning("{Owner} is still in the premium channel: "
                + "{Why}", row.Owner, ex.Message);
            return false;
        }
        db.ChannelLinks.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private bool IsPremiumChannel(JoinRequest request) =>
        request.ChatId.ToString(CultureInfo.InvariantCulture)
        == _options.PremiumChannel;
}
