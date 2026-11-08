using System.Security.Cryptography;
using System.Text;
using Foresight.Api.Accounts;
using Foresight.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Mail;

/// <summary>
/// The "ConfirmationMail" section: how often one address may be sent a
/// confirmation link.
/// </summary>
public sealed class ConfirmationMailOptions
{
    public const string SectionName = "ConfirmationMail";

    public int CooldownMinutes { get; set; } = 10;
    public int MaxPerDay { get; set; } = 5;
}

/// <summary>
/// Spaces out the confirmation mails to one address. Anyone may ask for a
/// new link without a password, so without this anyone could make us mail
/// an address as often as they liked: its owner gets flooded, the mail
/// plan runs out, and the domain's name with the mail providers suffers
/// for every real user.
/// </summary>
/// <remarks>
/// The counts live in the database, like the login lock, so a restart of
/// the app does not hand out a fresh round of mails.
/// </remarks>
public sealed class ConfirmationMailThrottle
{
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _cooldown;
    private readonly int _maxPerDay;

    // One question at a time, so two requests for one address cannot both
    // take the last mail of the round.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ConfirmationMailThrottle(
        IDbContextFactory<AppDbContext> db,
        IOptions<ConfirmationMailOptions> options,
        TimeProvider clock)
    {
        _db = db;
        _clock = clock;
        _cooldown = TimeSpan.FromMinutes(
            Math.Max(0, options.Value.CooldownMinutes));
        _maxPerDay = Math.Max(1, options.Value.MaxPerDay);
    }

    /// <summary>
    /// Null when a mail to this address may go out now, and then it is
    /// counted; otherwise how long until the next one may. A mail that then
    /// fails to send has still used its turn.
    /// </summary>
    public async Task<TimeSpan?> TryTakeAsync(
        string email, CancellationToken ct = default)
    {
        var key = KeyFor(email);
        var now = _clock.GetUtcNow().UtcDateTime;

        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _db.CreateDbContextAsync(ct);

            // A round that began a day ago or more is over. Deleting it lets
            // the address start a new one, and keeps the table small.
            await db.MailThrottle.Where(r => r.FirstUtc <= now - Day)
                .ExecuteDeleteAsync(ct);

            var row = await db.MailThrottle.FindAsync([key], ct);
            if (row is null)
            {
                db.MailThrottle.Add(new MailThrottleRow
                    { Key = key, Count = 1, FirstUtc = now, LastUtc = now });
            }
            else
            {
                var wait = row.Count >= _maxPerDay
                    ? row.FirstUtc + Day - now
                    : row.LastUtc + _cooldown - now;
                if (wait > TimeSpan.Zero)
                    return wait;
                row.Count++;
                row.LastUtc = now;
            }
            await db.SaveChangesAsync(ct);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    // The same form of the address that every table uses, then hashed.
    private static string KeyFor(string email) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(UserStore.Canonical(email))));
}
