namespace Foresight.Shared;

/// <summary>
/// What the subscribe page needs to draw PayPal's button. None of it is
/// secret: the client id and the plan ids are visible in every browser
/// that shows the button.
/// </summary>
/// <param name="ClientId">Empty while PayPal is not set up.</param>
public sealed record PlansDto(
    string ClientId, string MonthlyPlanId, string YearlyPlanId);
