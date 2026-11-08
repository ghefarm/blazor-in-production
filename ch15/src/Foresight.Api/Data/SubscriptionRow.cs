namespace Foresight.Api.Data;

/// <summary>One PayPal subscription, as the database keeps it.</summary>
public sealed class SubscriptionRow
{
    public int Id { get; set; }

    // PayPal's id ("I-..."), the only handle for asking PayPal about it.
    public string PayPalId { get; set; } = "";

    // The owning account, in the form UserStore.Canonical gives an email.
    public string Owner { get; set; } = "";

    public string Status { get; set; } = "";
    public string PlanId { get; set; } = "";
    public DateTime? PaidThroughUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public DateTime CheckedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
