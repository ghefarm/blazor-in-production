namespace Foresight.Shared;

/// <summary>One forecast for one event, as the API sends it.</summary>
public sealed record ForecastDto(
    string EventId,
    string Feed,
    string Title,
    DateTime StartsUtc,
    string Outcome,
    double Probability,
    bool IsPremium);
