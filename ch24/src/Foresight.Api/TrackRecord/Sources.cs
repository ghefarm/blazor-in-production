using Foresight.Shared;

namespace Foresight.Api.TrackRecord;

/// <summary>The forecasts that really went out.</summary>
public interface IPostedForecasts
{
    /// <summary>Every posted forecast on record, in any order.</summary>
    Task<IReadOnlyList<ForecastDto>> ReadAsync(CancellationToken ct = default);
}

/// <summary>
/// What really happened, from a source that is not the feed: a weather
/// archive, a timetable's log. It names events its own way.
/// </summary>
public interface IOutcomeSource
{
    /// <summary>
    /// Every event of the feed between the two days whose outcome is known,
    /// forecast or not.
    /// </summary>
    Task<IReadOnlyList<RecordedOutcome>> GetAsync(string feed,
        DateOnly from, DateOnly to, CancellationToken ct = default);
}

/// <summary>One event as the outcome source reports it.</summary>
/// <param name="Name">The source's spelling, not our title.</param>
/// <param name="StartsLocal">The local time of the place, not UTC.</param>
public sealed record RecordedOutcome(
    string Feed, string Name, DateTime StartsLocal, bool Happened);
