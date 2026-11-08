namespace Foresight.Billing;

/// <summary>
/// One stored subscription, and the only place that says what its status
/// means for the account that owns it.
/// </summary>
/// <param name="PaidThroughUtc">
/// The end of the period paid for, from PayPal's next billing time. PayPal
/// stops reporting it once a subscription is cancelled, so the last value
/// read while it was active is kept.
/// </param>
/// <param name="RevokedUtc">
/// When a payment was refunded or reversed. Access ends at once and stays
/// ended: PayPal may call the subscription ACTIVE for a while longer.
/// </param>
/// <param name="CheckedUtc">When PayPal was last asked about it.</param>
public sealed record SubscriptionRecord(
    string Owner,
    string Id,
    string Status,
    string PlanId,
    DateTime? PaidThroughUtc,
    DateTime? RevokedUtc,
    DateTime CheckedUtc)
{
    // How long past the paid period an ACTIVE subscription still counts. A
    // few days cover a slow payment or a late event. Without a limit, a
    // subscription whose payments stopped would unlock premium forever.
    public static readonly TimeSpan Grace = TimeSpan.FromDays(3);

    public bool Revoked => RevokedUtc is not null;

    /// <summary>Whether it unlocks premium at that moment.</summary>
    public bool IsEntitled(DateTime nowUtc) =>
        !Revoked
        && ((Status == PayPalStatus.Active
                && (PaidThroughUtc is null || PaidThroughUtc > nowUtc - Grace))
            || (Status == PayPalStatus.Cancelled && PaidThroughUtc > nowUtc));

    /// <summary>ACTIVE, but past its grace: a renewal may be missing.</summary>
    public bool LooksLapsed(DateTime nowUtc) =>
        !Revoked && Status == PayPalStatus.Active && !IsEntitled(nowUtc);

    /// <summary>Whether PayPal could still take money for it.</summary>
    public bool IsBillable =>
        Status is PayPalStatus.Approved or PayPalStatus.Active
            or PayPalStatus.Suspended;

    /// <summary>The end of premium access, or null when it ends now.</summary>
    public DateTime? PremiumUntil(DateTime nowUtc) =>
        IsEntitled(nowUtc) ? PaidThroughUtc : null;
}

/// <summary>Where subscriptions are kept. Chapter 8 keeps them in the
/// database; tests use <see cref="InMemorySubscriptionStore"/>.</summary>
public interface ISubscriptionStore
{
    /// <summary>
    /// Records what PayPal says. The first call decides the owner; later
    /// calls only move the status, the plan and the paid period.
    /// </summary>
    Task UpsertAsync(
        string owner,
        PayPalSubscription subscription,
        CancellationToken ct = default);

    /// <summary>
    /// The owner's subscription that matters most: one that unlocks premium
    /// if there is one, else the one changed last.
    /// </summary>
    Task<SubscriptionRecord?> FindCurrentAsync(
        string owner, CancellationToken ct = default);

    /// <summary>Notes that PayPal is about to be asked about it.</summary>
    Task TouchAsync(string subscriptionId, CancellationToken ct = default);
}
