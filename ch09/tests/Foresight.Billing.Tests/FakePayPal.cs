using Microsoft.AspNetCore.Http;

namespace Foresight.Billing.Tests;

/// <summary>PayPal as a test double: it answers from a dictionary.</summary>
internal sealed class FakePayPal : IPayPalClient
{
    private readonly Dictionary<string, PayPalSubscription> _subscriptions =
        new();

    public bool VouchesForEvents { get; set; } = true;
    public bool CancelFails { get; set; }
    public bool GetFails { get; set; }
    public Dictionary<string, string> SaleToSubscription { get; } = new();
    public List<string> Cancelled { get; } = new();
    public int VerifyCalls { get; private set; }
    public int GetCalls { get; private set; }

    public void Set(PayPalSubscription subscription) =>
        _subscriptions[subscription.Id] = subscription;

    public Task<PayPalSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        GetCalls++;
        if (GetFails)
            throw new PayPalException("PayPal could not be reached.");
        return Task.FromResult(
            _subscriptions.GetValueOrDefault(subscriptionId));
    }

    public Task<string?> GetSaleSubscriptionIdAsync(
        string saleId, CancellationToken ct = default) =>
        Task.FromResult(SaleToSubscription.GetValueOrDefault(saleId));

    public Task CancelSubscriptionAsync(
        string subscriptionId, string reason, CancellationToken ct = default)
    {
        if (CancelFails)
            throw new PayPalException("PayPal could not be reached.");
        Cancelled.Add(subscriptionId);

        // As PayPal does: the status changes, and there is no next billing
        // time any more.
        if (_subscriptions.TryGetValue(subscriptionId, out var s))
            _subscriptions[subscriptionId] = s with
            {
                Status = PayPalStatus.Cancelled,
                NextBillingUtc = null,
            };
        return Task.CompletedTask;
    }

    public Task<bool> VerifyWebhookAsync(
        IHeaderDictionary headers,
        string rawBody,
        CancellationToken ct = default)
    {
        VerifyCalls++;
        return Task.FromResult(VouchesForEvents);
    }
}
