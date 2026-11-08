namespace Foresight.Api.Feeds;

/// <summary>The "Feed" section of the settings.</summary>
public sealed class FeedOptions
{
    public const string SectionName = "Feed";

    // Paid data feeds sell a number of requests per month. The sample feed
    // counts its requests the same way, so the app has to live with a limit.
    public int MonthlyLimit { get; set; } = 500;
}
