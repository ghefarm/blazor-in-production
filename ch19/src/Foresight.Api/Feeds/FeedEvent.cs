namespace Foresight.Api.Feeds;

/// <summary>A scheduled event as the feed reports it.</summary>
/// <param name="Score">
/// The feed's own indicator for the outcome, from 0 to 100. The forecast is
/// worked out from it.
/// </param>
public sealed record FeedEvent(
    string Id,
    string Feed,
    string Title,
    string Outcome,
    DateTime StartsUtc,
    int Score);
