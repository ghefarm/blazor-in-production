namespace Foresight.Billing.Tests;

/// <summary>The two edges of the grace after the paid period.</summary>
[Trait("Chapter", "8")]
public sealed class GraceTests
{
    private static readonly DateTime Now =
        new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static SubscriptionRecord Sub(
        string status, DateTime? paidThrough) =>
        new("ann@example.com", "I-1", status, "P-MONTHLY",
            paidThrough, RevokedUtc: null, CheckedUtc: Now);

    [Fact]
    public void Active_TwoDaysPastThePaidPeriod_StillHasPremium() =>
        Assert.True(Sub(PayPalStatus.Active, Now.AddDays(-2)).IsEntitled(Now));

    [Fact]
    public void Active_FourDaysPastThePaidPeriod_HasNot_AndLooksLapsed()
    {
        var sub = Sub(PayPalStatus.Active, Now.AddDays(-4));

        Assert.False(sub.IsEntitled(Now));
        Assert.True(sub.LooksLapsed(Now));
    }
}
