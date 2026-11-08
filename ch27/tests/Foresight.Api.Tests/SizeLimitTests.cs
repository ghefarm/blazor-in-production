using System.Net;
using System.Text;
using Foresight.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// The webhook's size limit, on Kestrel, the real web server. The in-memory
/// test server does not enforce the limit at all.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "26")]
public sealed class SizeLimitTests
{
    private const string Url = "/api/Subscription/webhook";
    private const string Activated = """
        {"event_type":"BILLING.SUBSCRIPTION.ACTIVATED",
         "resource":{"id":"I-1"}}
        """;

    // A real event followed by 64 KB of spaces: still valid JSON.
    private static readonly string Oversized =
        Activated + new string(' ', 64 * 1024);

    private readonly ApiFactory _factory;

    public SizeLimitTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BodyOver64KB_IsRefusedByKestrel_BeforeOurCodeRuns()
    {
        var paypal = new CountingPayPal();
        using var kestrel = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s =>
                s.AddSingleton<IPayPalClient>(paypal)));
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        var client = kestrel.CreateClient();

        var response = await client.PostAsync(Url, Json(Oversized));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            response.StatusCode);
        Assert.Equal(0, paypal.Calls);
    }

    private static StringContent Json(string body) =>
        new(body, Encoding.UTF8, "application/json");

    /// <summary>PayPal as a stub that counts every question it gets.</summary>
    private sealed class CountingPayPal : IPayPalClient
    {
        public int Calls { get; private set; }

        public Task<bool> VerifyWebhookAsync(IHeaderDictionary headers,
            string rawBody, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(true);
        }

        public Task<PayPalSubscription?> GetSubscriptionAsync(
            string subscriptionId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<PayPalSubscription?>(null);
        }

        public Task<string?> GetSaleSubscriptionIdAsync(
            string saleId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task CancelSubscriptionAsync(string subscriptionId,
            string reason, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
