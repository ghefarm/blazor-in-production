using Microsoft.AspNetCore.Http;

namespace Foresight.Billing.Tests;

/// <summary>Runs that stop between two steps, and the run after them.</summary>
[Trait("Chapter", "12")]
public sealed class CrashTests
{
    private const string Ann = "ann@example.com";

    private static readonly DateTime PaidThrough =
        DateTime.UtcNow.AddDays(20);

    private readonly FakePayPal _paypal = new();
    private readonly InMemorySubscriptionStore _store = new();
    private readonly SubscriptionManager _manager;

    public CrashTests() =>
        _manager = new(_paypal, _store, ["P-MONTHLY", "P-YEARLY"]);

    private Task<bool> Handle(string type, string resource) =>
        _manager.HandleWebhookAsync(new HeaderDictionary(),
            $$"""{"event_type":"{{type}}","resource":{{resource}}}""");

    private async Task Active(string id)
    {
        var sub = new PayPalSubscription(id, PayPalStatus.Active,
            "P-MONTHLY", Ann, PaidThrough);
        _paypal.Set(sub);
        await _store.UpsertAsync(Ann, sub);
    }

    [Fact]
    public async Task ARefund_StoppedAfterTheRevoke_IsFinishedByTheRetry()
    {
        await Active("I-ANN");
        _paypal.SaleToSubscription["S-1"] = "I-ANN";
        const string Sale = """{"id":"R-1","sale_id":"S-1"}""";

        // First delivery: access ends, then PayPal cannot be reached.
        _paypal.CancelFails = true;
        await Assert.ThrowsAsync<PayPalException>(
            () => Handle("PAYMENT.SALE.REFUNDED", Sale));
        Assert.True((await _store.FindAsync("I-ANN"))!.Revoked);
        Assert.Empty(_paypal.Cancelled);

        // The endpoint answered 502, so PayPal sends the event again.
        _paypal.CancelFails = false;
        Assert.True(await Handle("PAYMENT.SALE.REFUNDED", Sale));

        Assert.Equal(["I-ANN"], _paypal.Cancelled);
        Assert.True((await _store.FindAsync("I-ANN"))!.Revoked);
    }

    [Fact]
    public async Task ACancel_StoppedBeforeTheReadBack_IsFinishedByTheWebhook()
    {
        await Active("I-ANN");

        // PayPal takes the cancel, then cannot be asked about it.
        _paypal.GetFails = true;
        Assert.Equal(1, await _manager.CancelAsync(Ann));
        Assert.Equal(PayPalStatus.Active,
            (await _store.FindAsync("I-ANN"))!.Status);

        // Later PayPal reports the cancellation on the webhook.
        _paypal.GetFails = false;
        Assert.True(await Handle("BILLING.SUBSCRIPTION.CANCELLED",
            """{"id":"I-ANN"}"""));

        var row = (await _store.FindAsync("I-ANN"))!;
        Assert.Equal(PayPalStatus.Cancelled, row.Status);
        Assert.Equal(PaidThrough, row.PaidThroughUtc);
    }
}
