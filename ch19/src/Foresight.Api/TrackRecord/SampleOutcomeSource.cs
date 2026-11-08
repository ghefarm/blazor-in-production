using Foresight.Api.Feeds;

namespace Foresight.Api.TrackRecord;

/// <summary>
/// A stand-in for a second source with what really happened. Like a real
/// one, it names the events its own way and keeps the local time of the
/// place, so the track record must find each outcome by its event.
/// </summary>
public sealed class SampleOutcomeSource : IOutcomeSource
{
    // The sample feed's six events, in its order, as this source names them.
    private static readonly Dictionary<string, string[]> Names = new()
    {
        ["weather"] =
            ["HAMBURG", "MUNICH", "ZURICH", "BERGEN", "LONDON", "VIENNA"],
        ["transit"] =
            ["512 Berlin", "7 Malmo", "204 Rome", "31 Airport", "88 Prague",
             "4 Harbour"],
    };

    // The places keep central European time: an hour ahead of UTC here.
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(1);

    // An outcome is known two hours after the start, not before.
    private static readonly TimeSpan KnownAfter = TimeSpan.FromHours(2);

    private readonly TimeProvider _clock;

    public SampleOutcomeSource(TimeProvider clock)
    {
        _clock = clock;
    }

    public Task<IReadOnlyList<RecordedOutcome>> GetAsync(
        string feed, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var outcomes = new List<RecordedOutcome>();
        var names = Names.GetValueOrDefault(feed) ?? [];
        for (var day = from; day <= to && names.Length > 0;
             day = day.AddDays(1))
        {
            var events = SampleEventSource.EventsOf(feed, day);
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.StartsUtc + KnownAfter > now)
                    continue;

                // The sample cheats on purpose: an outcome follows the
                // feed's own score, so the forecasts have some skill to
                // measure. A real archive knows nothing of our feed.
                var happened = Dice(e.Id) < e.Score;
                outcomes.Add(new RecordedOutcome(feed,
                    names[i % names.Length], e.StartsUtc + LocalOffset,
                    happened));
            }
        }
        return Task.FromResult<IReadOnlyList<RecordedOutcome>>(outcomes);
    }

    // A number from 0 to 99 that depends on the id alone (FNV-1a, with a
    // different start than the feed's score, so the two do not agree).
    private static int Dice(string id)
    {
        uint hash = 84696351;
        foreach (var c in id)
            hash = (hash ^ c) * 16777619;
        return (int)(hash % 100);
    }
}
