namespace Foresight.Api.Feeds;

/// <summary>
/// A feed that needs no account and no key: the same day always gives the
/// same events, so every reader sees the numbers printed in the book.
/// </summary>
public sealed class SampleEventSource : IEventSource
{
    private const int EventsPerDay = 6;

    // Per feed: the title of each event and the outcome it forecasts.
    private static readonly Dictionary<string, (string Title, string Outcome)[]>
        Catalog = new()
        {
            ["weather"] =
            [
                ("Rain in Hamburg", "Rain"),
                ("Sun in Munich", "Sun"),
                ("Snow in Zurich", "Snow"),
                ("Storm in Bergen", "Storm"),
                ("Fog in London", "Fog"),
                ("Frost in Vienna", "Frost"),
            ],
            ["transit"] =
            [
                ("Train 512 to Berlin", "On time"),
                ("Ferry 7 to Malmo", "On time"),
                ("Flight 204 to Rome", "On time"),
                ("Bus 31 to the airport", "On time"),
                ("Train 88 to Prague", "On time"),
                ("Tram 4 to the harbour", "On time"),
            ],
        };

    public IReadOnlyCollection<string> Feeds => Catalog.Keys;

    public Task<IReadOnlyList<FeedEvent>> GetEventsAsync(
        string feed, DateOnly day, CancellationToken ct = default) =>
        Task.FromResult(EventsOf(feed, day));

    /// <summary>The events of one day: six, two hours apart.</summary>
    internal static IReadOnlyList<FeedEvent> EventsOf(string feed, DateOnly day)
    {
        if (!Catalog.TryGetValue(feed, out var entries))
            return [];

        var start = day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc);
        return Enumerable.Range(0, EventsPerDay)
            .Select(i =>
            {
                var id = $"{feed}-{day:yyyyMMdd}-{i + 1}";
                var (title, outcome) = entries[i % entries.Length];
                return new FeedEvent(id, feed, title, outcome,
                    start.AddHours(2 * i), ScoreOf(id));
            })
            .ToList();
    }

    // A score from the id alone, the same on every machine and every run.
    // string.GetHashCode would not do: .NET gives it a new seed per process.
    private static int ScoreOf(string id)
    {
        uint hash = 2166136261; // FNV-1a
        foreach (var c in id)
            hash = (hash ^ c) * 16777619;
        return (int)(hash % 101);
    }
}
