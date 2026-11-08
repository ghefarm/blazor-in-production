using Foresight.Api.Data;

namespace Foresight.Api.TrackRecord;

/// <summary>
/// Finds the outcome of a forecast. The outcome source spells events its
/// own way and keeps local time, so an outcome is found by its event, not by
/// a table of names that would break with every new event.
/// </summary>
public static class OutcomeFinder
{
    // Wider than the gap between local time and UTC, and far narrower than
    // the day between two events of the same name.
    public static readonly TimeSpan Window = TimeSpan.FromHours(3);

    public static RecordedOutcome? Find(
        RecordRow forecast, IEnumerable<RecordedOutcome> outcomes)
    {
        var title = Words(forecast.Title);
        var fits = outcomes
            .Where(o => o.Feed == forecast.Feed
                && (o.StartsLocal - forecast.StartsUtc).Duration() <= Window
                && Words(o.Name).IsSubsetOf(title))
            .Take(2)
            .ToList();

        // Two outcomes that fit mean the names did not decide it. A
        // forecast left open is better than one scored against the wrong
        // event.
        return fits.Count == 1 ? fits[0] : null;
    }

    // "Rain in Hamburg" and "HAMBURG" meet in the word "hamburg".
    private static HashSet<string> Words(string text) => text
        .ToLowerInvariant()
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet();
}
