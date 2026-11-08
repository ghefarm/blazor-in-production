namespace Foresight.Shared;

/// <summary>
/// A what-if question: the forecast for one event if the feed's indicator
/// for it were Score (0 to 100) instead of the feed's own number.
/// </summary>
public sealed record WhatIfRequest(string Feed, string EventId, int Score);

/// <summary>The answer to a what-if question.</summary>
/// <param name="Direction">
/// For everyone: "likely", "open" or "unlikely" at the caller's number.
/// </param>
/// <param name="Probability">
/// For a subscriber only: the forecast at the caller's number.
/// </param>
/// <param name="HiddenPremium">
/// For everyone else: how many premium forecasts the event has, at the
/// feed's own number. Null when that was not worked out.
/// </param>
public sealed record WhatIfDto(
    string Direction,
    double? Probability,
    int? HiddenPremium);
