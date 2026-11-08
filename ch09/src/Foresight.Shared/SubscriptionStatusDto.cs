namespace Foresight.Shared;

/// <summary>The signed-in account's subscription, for its own page.</summary>
/// <param name="Status">PayPal's status; null without one.</param>
/// <param name="PremiumUntil">The end of premium access, or null.</param>
public sealed record SubscriptionStatusDto(
    string? Status, bool HasPremium, DateTime? PremiumUntil);
