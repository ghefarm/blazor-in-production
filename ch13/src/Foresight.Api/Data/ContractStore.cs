using Foresight.Api.Accounts;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Data;

/// <summary>
/// The records of a contract: the consents given before a payment, and the
/// contract mails sent after it.
/// </summary>
public sealed class ContractStore
{
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly ISubscriptionStore _subscriptions;
    private readonly TimeProvider _clock;

    public ContractStore(IDbContextFactory<AppDbContext> db,
        ISubscriptionStore subscriptions, TimeProvider clock)
    {
        _db = db;
        _subscriptions = subscriptions;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Stores the current consent text for the account.</summary>
    public async Task RecordConsentAsync(
        string owner, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        db.Consents.Add(new ConsentRow
        {
            Owner = UserStore.Canonical(owner),
            Version = PremiumTerms.ConsentVersion,
            Text = PremiumTerms.ConsentText,
            ConsentedUtc = Now,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The account's latest consent, or null.</summary>
    public async Task<ConsentRow?> LatestConsentAsync(
        string owner, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(owner);
        await using var db = await _db.CreateDbContextAsync(ct);
        return await db.Consents.AsNoTracking()
            .Where(c => c.Owner == key)
            .OrderByDescending(c => c.ConsentedUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Marks the contract mail of a subscription as sent. False when it was
    /// marked before, by this call or by another one at the same moment.
    /// </summary>
    public async Task<bool> TryMarkMailAsync(
        string payPalId, string owner, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        db.ContractMails.Add(new ContractMailRow
        {
            PayPalId = payPalId,
            Owner = UserStore.Canonical(owner),
            SentUtc = Now,
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    /// <summary>Takes a mark back, so the mail is tried again later.</summary>
    public async Task UnmarkMailAsync(
        string payPalId, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.ContractMails.Where(m => m.PayPalId == payPalId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Removes the account's records, except those of a contract that still
    /// runs: a subscription PayPal may still bill keeps its contract mail,
    /// and the account keeps its consents. Safe to repeat.
    /// </summary>
    public async Task ForgetAsync(
        string owner, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(owner);
        // Forget only what has been cancelled, by chapter 8's rule.
        var running = (await _subscriptions.FindAllAsync(key, ct))
            .Where(s => s.IsBillable)
            .Select(s => s.Id)
            .ToList();
        await using var db = await _db.CreateDbContextAsync(ct);
        if (running.Count == 0)
            await db.Consents.Where(c => c.Owner == key)
                .ExecuteDeleteAsync(ct);
        await db.ContractMails
            .Where(m => m.Owner == key && !running.Contains(m.PayPalId))
            .ExecuteDeleteAsync(ct);
    }
}
