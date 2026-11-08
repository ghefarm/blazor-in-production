using Microsoft.AspNetCore.Http;

namespace Foresight.Billing.Tests;

/// <summary>The rules of the webhook spec that carry chapter 9.</summary>
[Trait("Chapter", "9")]
public sealed class WebhookRulesTests
{
    private const string Owner = "ann@example.com";

    private readonly FakePayPal _paypal = new();
    private readonly InMemorySubscriptionStore _store = new();
    private readonly SubscriptionManager _manager;

    public WebhookRulesTests() =>
        _manager = new(_paypal, _store, ["P-MONTHLY", "P-YEARLY"]);

    private Task<bool> Handle(string body) =>
        _manager.HandleWebhookAsync(new HeaderDictionary(), body);

    private static string Evt(string type, string resource) =>
        $$"""{"event_type":"{{type}}","resource":{{resource}}}""";

    private static string Activated(string id) =>
        Evt("BILLING.SUBSCRIPTION.ACTIVATED", $$"""{"id":"{{id}}"}""");

    private static PayPalSubscription Sub(string id, string status) =>
        new(id, status, "P-MONTHLY", Owner, null);

    [Fact]
    public async Task ForgedEvent_IsRefused_AndChangesNothing()
    {
        _paypal.VouchesForEvents = false;
        _paypal.Set(Sub("I-1", "ACTIVE"));

        Assert.False(await Handle(Activated("I-1")));

        Assert.Equal(0, _store.Count);
        Assert.Equal(0, _paypal.GetCalls);
    }

    [Fact]
    public async Task LateEvent_CannotMoveTheRowBackwards()
    {
        _paypal.Set(Sub("I-1", "ACTIVE"));
        await Handle(Activated("I-1"));

        _paypal.Set(Sub("I-1", "CANCELLED"));
        await Handle(Activated("I-1")); // an old event, arriving late

        Assert.Equal("CANCELLED", (await _store.FindAsync("I-1"))!.Status);
    }

    [Fact]
    public async Task UnreadableString_IsAcknowledged_WithoutAskingPayPal()
    {
        // Valid JSON, but half of a UTF-16 pair is not text .NET can read.
        var body = """{"event_type":"\ud800","resource":{"id":"I-1"}}""";

        Assert.True(await Handle(body));

        Assert.Equal(0, _paypal.VerifyCalls);
    }
}
