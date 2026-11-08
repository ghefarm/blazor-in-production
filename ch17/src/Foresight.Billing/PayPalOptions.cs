namespace Foresight.Billing;

/// <summary>The "PayPal" section of the settings.</summary>
public sealed class PayPalOptions
{
    public const string SectionName = "PayPal";

    // The sandbox while you build. The host's settings switch to the live
    // address, https://api-m.paypal.com/, together with live credentials.
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com/";

    // A REST app's credentials. The secret comes only from the host.
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // The webhook registered for our address. Without it every event is
    // refused: there would be no way to tell PayPal's call from anyone's.
    public string WebhookId { get; set; } = "";

    public string MonthlyPlanId { get; set; } = "";
    public string YearlyPlanId { get; set; } = "";

    /// <summary>The plans that unlock premium, without empty ids.</summary>
    public IReadOnlyCollection<string> PremiumPlanIds =>
        new[] { MonthlyPlanId, YearlyPlanId }
            .Where(id => id.Length > 0)
            .ToArray();
}
