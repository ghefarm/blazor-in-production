namespace Foresight.Billing;

/// <summary>What PayPal says about one subscription.</summary>
public sealed record PayPalSubscription(
    string Id,
    string Status,
    string PlanId,
    string? CustomId,
    DateTime? NextBillingUtc);

public sealed class PayPalException : Exception
{
    public PayPalException(string message) : base(message) { }

    public PayPalException(string message, Exception inner)
        : base(message, inner) { }
}

/// <summary>The few PayPal calls the billing code needs.</summary>
public interface IPayPalClient
{
    /// <summary>The subscription as PayPal has it, or null.</summary>
    Task<PayPalSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct = default);
}
