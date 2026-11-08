namespace Foresight.Billing;

public sealed partial class SubscriptionManager
{
    /// <summary>
    /// Records a subscription that a signed-in visitor says is theirs, right
    /// after PayPal approved it in their browser.
    /// </summary>
    /// <param name="account">The signed-in account's email.</param>
    /// <param name="subscriptionId">The id PayPal gave the page.</param>
    /// <param name="ct">Cancels the PayPal and store calls.</param>
    /// <remarks>
    /// Errors from PayPal are not caught. The payment may still be fine, and
    /// the webhook records the subscription when PayPal reports it.
    /// </remarks>
    public async Task<ClaimOutcome> ClaimAsync(
        string account,
        string subscriptionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentNullException.ThrowIfNull(subscriptionId);

        // The id comes from the page, so it is only the visitor's word. A
        // malformed one never reaches PayPal.
        if (!IsValidId(subscriptionId))
            return ClaimOutcome.NotAnId;

        // Check 1: PayPal knows the subscription.
        var subscription =
            await _paypal.GetSubscriptionAsync(subscriptionId, ct);
        if (subscription is null || subscription.Id != subscriptionId)
            return ClaimOutcome.NotFound;

        // Check 2: it is a subscription to one of our plans.
        if (!_premiumPlanIds.Contains(subscription.PlanId))
            return ClaimOutcome.WrongPlan;

        // Check 3: the tag our page gave PayPal names this account. The
        // email of the PayPal account plays no part: anyone may pay with
        // any PayPal account.
        if (subscription.CustomId is not { } tag
            || !SameAccount(tag, account))
            return ClaimOutcome.NotYours;

        await RecordAsync(account, subscription, ct);
        return ClaimOutcome.Recorded;
    }

    // Emails are compared the way the stores keep them: trimmed, and with
    // case ignored.
    private static bool SameAccount(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
