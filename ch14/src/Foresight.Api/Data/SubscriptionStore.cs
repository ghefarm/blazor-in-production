using Foresight.Api.Accounts;
using Foresight.Billing;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Data;

/// <summary>The subscriptions, in the one database.</summary>
public sealed class SubscriptionStore : ISubscriptionStore
{
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly TimeProvider _clock;

    public SubscriptionStore(
        IDbContextFactory<AppDbContext> db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task UpsertAsync(
        string owner, PayPalSubscription s, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        if (await UpdateAsync() > 0) return;
        db.Subscriptions.Add(new SubscriptionRow
        {
            PayPalId = s.Id,
            Owner = UserStore.Canonical(owner),
            Status = s.Status,
            PlanId = s.PlanId,
            PaidThroughUtc = s.NextBillingUtc,
            CheckedUtc = Now,
            CreatedUtc = Now,
            UpdatedUtc = Now,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two events for a new subscription can arrive together, and
            // the other one inserted first. Then the row exists: update it.
            db.ChangeTracker.Clear();
            if (await UpdateAsync() == 0) throw;
        }

        // The owner is never changed here: the first record decides it.
        // PayPal stops sending a billing date once a subscription is
        // cancelled, so the last one known is kept.
        Task<int> UpdateAsync() => db.Subscriptions
            .Where(r => r.PayPalId == s.Id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, s.Status)
                .SetProperty(r => r.PlanId, s.PlanId)
                .SetProperty(r => r.PaidThroughUtc,
                    r => s.NextBillingUtc ?? r.PaidThroughUtc)
                .SetProperty(r => r.UpdatedUtc, Now), ct);
    }

    public async Task RevokeAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.Subscriptions
            .Where(r => r.PayPalId == subscriptionId && r.RevokedUtc == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.RevokedUtc, Now)
                .SetProperty(r => r.UpdatedUtc, Now), ct);
    }

    public async Task<SubscriptionRecord?> FindAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        var row = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PayPalId == subscriptionId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<SubscriptionRecord?> FindCurrentAsync(
        string owner, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(owner);
        await using var db = await _db.CreateDbContextAsync(ct);
        var rows = await db.Subscriptions.AsNoTracking()
            .Where(r => r.Owner == key)
            .ToListAsync(ct);
        return rows
            .Select(r => (Row: r, Record: ToRecord(r)))
            .OrderByDescending(x => x.Record.IsEntitled(Now))
            .ThenByDescending(x => x.Row.UpdatedUtc)
            .Select(x => x.Record)
            .FirstOrDefault();
    }

    public async Task TouchAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.Subscriptions
            .Where(r => r.PayPalId == subscriptionId)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.CheckedUtc, Now), ct);
    }

    public async Task<IReadOnlyList<SubscriptionRecord>> FindAllAsync(
        string owner, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(owner);
        await using var db = await _db.CreateDbContextAsync(ct);
        var rows = await db.Subscriptions.AsNoTracking()
            .Where(r => r.Owner == key)
            .ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task ForgetAsync(
        string owner, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(owner);
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.Subscriptions.Where(r => r.Owner == key)
            .ExecuteDeleteAsync(ct);
    }

    public async Task ForgetOneAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.Subscriptions.Where(r => r.PayPalId == subscriptionId)
            .ExecuteDeleteAsync(ct);
    }

    // SQLite hands dates back without a kind; they were stored as UTC.
    private static SubscriptionRecord ToRecord(SubscriptionRow r) =>
        new(r.Owner, r.PayPalId, r.Status, r.PlanId,
            Utc(r.PaidThroughUtc), Utc(r.RevokedUtc), Utc(r.CheckedUtc)!.Value);

    private static DateTime? Utc(DateTime? d) =>
        d is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;
}
