using Microsoft.AspNetCore.Http;

namespace Foresight.Billing.Tests;

/// <summary>Whose subscription it is: PayPal's tag decides.</summary>
[Trait("Chapter", "10")]
public sealed class ClaimOwnerTests
{
    private const string Ann = "ann@example.com";
    private const string Mallory = "mallory@example.com";

    private readonly FakePayPal _paypal = new();
    private readonly InMemorySubscriptionStore _store = new();
    private readonly SubscriptionManager _manager;

    public ClaimOwnerTests() =>
        _manager = new(_paypal, _store, ["P-MONTHLY", "P-YEARLY"]);

    // A real subscription on our monthly plan, with the tag our page set.
    private static PayPalSubscription Sub(string id, string? tag) =>
        new(id, PayPalStatus.Active, "P-MONTHLY", tag,
            DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task AnotherAccountsSubscription_IsNotYours()
    {
        // Mallory has seen Ann's subscription id, on a shared screen or in
        // a screenshot, and sends it as his own.
        _paypal.Set(Sub("I-ANN", Ann));

        var outcome = await _manager.ClaimAsync(Mallory, "I-ANN");

        Assert.Equal(ClaimOutcome.NotYours, outcome);
        Assert.Equal(0, _store.Count);
    }

    [Fact]
    public async Task AnUntaggedSubscription_CannotBeClaimed()
    {
        // Made somewhere else, for example from a direct PayPal link: PayPal
        // knows it, but nothing in it names an account.
        _paypal.Set(Sub("I-LINK", tag: null));

        var outcome = await _manager.ClaimAsync(Ann, "I-LINK");

        Assert.Equal(ClaimOutcome.NotYours, outcome);
        Assert.Equal(0, _store.Count);
    }

    [Fact]
    public async Task TheClaim_AndTheWebhook_KeepOneRow()
    {
        _paypal.Set(Sub("I-ANN", Ann));

        await _manager.ClaimAsync(Ann, "I-ANN");
        await _manager.HandleWebhookAsync(new HeaderDictionary(),
            """
            {"event_type":"BILLING.SUBSCRIPTION.ACTIVATED",
             "resource":{"id":"I-ANN"}}
            """);

        Assert.Equal(1, _store.Count);
        Assert.Equal(Ann, (await _store.FindAsync("I-ANN"))!.Owner);
    }
}
