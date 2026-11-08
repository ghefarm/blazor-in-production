namespace Foresight.Api.Feeds;

/// <summary>
/// Where the events come from. Foresight ships a sample feed; a real
/// provider would be another class behind this interface.
/// </summary>
public interface IEventSource
{
    /// <summary>The feeds this source knows, such as "weather".</summary>
    IReadOnlyCollection<string> Feeds { get; }

    /// <summary>The events of one feed on one day, in start order.</summary>
    /// <exception cref="FeedLimitException">
    /// The month's requests are used up.
    /// </exception>
    Task<IReadOnlyList<FeedEvent>> GetEventsAsync(
        string feed, DateOnly day, CancellationToken ct = default);
}

/// <summary>The feed refused: this month's requests are used up.</summary>
public sealed class FeedLimitException : Exception
{
    public FeedLimitException(string message) : base(message) { }
}
