using System.Diagnostics;
using System.Net;
using System.Text;
using Foresight.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// The webhook with HTTP on both sides: a request into the real API, and
/// the real PayPal client's requests out of it, to a fake PayPal server or
/// to nowhere.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "26")]
public sealed class PayPalOverHttpTests
{
    private readonly ApiFactory _factory;

    public PayPalOverHttpTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TheWebhook_GoesThroughTheRealPayPalClient()
    {
        // The owner is a real account: for nobody, nothing is stored.
        var owner = NewOwner();
        await _factory.RegisterConfirmedAsync(owner);
        var paypal = new FakePayPalServer();
        var id = paypal.AddSubscription("ACTIVE", "P-MONTHLY", owner);
        using var api = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s =>
            {
                // The real client stays; only the network below it changes.
                s.AddHttpClient(PayPalClient.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(paypal.Handler);
                s.PostConfigure<PayPalOptions>(o => o.WebhookId = "WH-1");
            }));

        var response = await api.CreateClient().SendAsync(Activated(id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["POST v1/oauth2/token",
             "POST v1/notifications/verify-webhook-signature",
             $"GET v1/billing/subscriptions/{id}"],
            paypal.Requests);
        var store = api.Services.GetRequiredService<ISubscriptionStore>();
        Assert.Equal(PayPalStatus.Active, (await store.FindAsync(id))?.Status);
    }

    [Fact]
    public async Task ACallThatSlipsPastTheFakes_FailsAtOnce()
    {
        // No fake at all: the real client calls the address that ApiFactory
        // set for PayPal, a port on this machine where nothing listens.
        using var api = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.PostConfigure<PayPalOptions>(
                o => o.WebhookId = "WH-1")));
        var clock = Stopwatch.StartNew();

        var response = await api.CreateClient()
            .SendAsync(Activated("I-NOWHERE1"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10),
            $"took {clock.Elapsed}, close to the client's own time-out");
    }

    private static string NewOwner() =>
        $"owner-{Guid.NewGuid():N}@example.test";

    // An event as PayPal sends it, with the headers its signature needs.
    private static HttpRequestMessage Activated(string subscriptionId)
    {
        var body = $$$"""
            {"event_type":"BILLING.SUBSCRIPTION.ACTIVATED",
             "resource":{"id":"{{{subscriptionId}}}"}}
            """;
        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/Subscription/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("PAYPAL-AUTH-ALGO", "SHA256withRSA");
        request.Headers.Add("PAYPAL-CERT-URL",
            "https://api.paypal.com/v1/notifications/certs/CERT-1");
        request.Headers.Add("PAYPAL-TRANSMISSION-ID", "T-1");
        request.Headers.Add("PAYPAL-TRANSMISSION-SIG", "c2lnbmF0dXJl");
        request.Headers.Add("PAYPAL-TRANSMISSION-TIME",
            "2026-11-04T10:00:00Z");
        return request;
    }
}
