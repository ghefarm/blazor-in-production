namespace Foresight.Shared;

/// <summary>
/// One feed's track record: every forecast that said yes, and how often
/// the outcome happened in all events of the period.
/// </summary>
/// <param name="Events">All events of the period at the outcome source,
/// forecast or not.</param>
/// <param name="EventsHappened">Of those, the events whose outcome
/// happened: the hit rate of saying yes to everything.</param>
public sealed record TrackRecordDto(
    string Feed,
    int Events,
    int EventsHappened,
    IReadOnlyList<RecordedForecastDto> Forecasts);

/// <summary>A posted forecast. Happened is null while it is open.</summary>
public sealed record RecordedForecastDto(
    string Title,
    DateTime StartsUtc,
    string Outcome,
    double Probability,
    bool IsPremium,
    bool? Happened);

/// <summary>What a refresh of the track record found.</summary>
/// <param name="Missing">Forecasts whose outcome the source should know
/// by now, but for which no outcome fits.</param>
public sealed record RefreshReport(
    int Forecasts,
    int Settled,
    int Open,
    IReadOnlyList<string> Missing);
