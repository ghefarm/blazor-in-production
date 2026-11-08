using Foresight.Billing;
using Microsoft.AspNetCore.Http;

namespace Foresight.Api.Tests;

/// <summary>
/// PayPal for the API tests: it answers from a dictionary, vouches for
/// every event, and cancels the way PayPal does. Or it is down.
/// </summary>
internal sealed class StubPayPal : IPayPalClient
{
    public Dictionary<string, PayPalSubscription> Subscriptions { get; }
        = new();
    public List<string> Cancelled { get; } = new();
    public bool Down { get; set; }

    public void Set(PayPalSubscription s) => Subscriptions[s.Id] = s;

    public Task<PayPalSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        ThrowIfDown();
        return Task.FromResult(
            Subscriptions.GetValueOrDefault(subscriptionId));
    }

    public Task CancelSubscriptionAsync(string subscriptionId,
        string reason, CancellationToken ct = default)
    {
        ThrowIfDown();
        Cancelled.Add(subscriptionId);
        if (Subscriptions.TryGetValue(subscriptionId, out var s))
            Subscriptions[subscriptionId] = s with
            {
                Status = PayPalStatus.Cancelled,
                NextBillingUtc = null,
            };
        return Task.CompletedTask;
    }

    public Task<bool> VerifyWebhookAsync(IHeaderDictionary headers,
        string rawBody, CancellationToken ct = default)
    {
        ThrowIfDown();
        return Task.FromResult(true);
    }

    public Task<string?> GetSaleSubscriptionIdAsync(
        string saleId, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private void ThrowIfDown()
    {
        if (Down)
            throw new PayPalException("PayPal could not be reached.");
    }
}
