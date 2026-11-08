namespace Foresight.Billing;

/// <summary>
/// Told every time a subscription is stored, by the claim or the webhook.
/// </summary>
public interface ISubscriptionListener
{
    /// <summary>Called after each write. May come many times.</summary>
    Task RecordedAsync(
        string owner,
        PayPalSubscription subscription,
        CancellationToken ct = default);
}
