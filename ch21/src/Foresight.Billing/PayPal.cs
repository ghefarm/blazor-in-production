using Microsoft.AspNetCore.Http;

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

    /// <summary>The subscription a sale paid for, or null if none.</summary>
    Task<string?> GetSaleSubscriptionIdAsync(
        string saleId, CancellationToken ct = default);

    /// <summary>Stops the subscription so it is not billed again.</summary>
    Task CancelSubscriptionAsync(
        string subscriptionId, string reason, CancellationToken ct = default);

    /// <summary>Asks PayPal whether a webhook call is really its own.</summary>
    Task<bool> VerifyWebhookAsync(
        IHeaderDictionary headers,
        string rawBody,
        CancellationToken ct = default);
}
