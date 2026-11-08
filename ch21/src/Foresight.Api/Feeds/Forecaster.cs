using Foresight.Shared;

namespace Foresight.Api.Feeds;

/// <summary>
/// Turns a feed event into a forecast. The model is simple on purpose: this
/// book is about everything around it.
/// </summary>
public static class Forecaster
{
    // Premium is the confident end of the scale. Chapter 24 shows why this
    // cut must never leak, not even as a count.
    public const double PremiumFrom = 0.75;

    public static ForecastDto For(FeedEvent e)
    {
        // A score of 50 means "no idea", so 50 percent. Every 12 points move
        // the probability by 10, kept between 5 and 95 percent.
        var p = Math.Clamp(0.5 + (e.Score - 50) / 120.0, 0.05, 0.95);
        p = Math.Round(p, 2);
        return new ForecastDto(e.Id, e.Feed, e.Title, e.StartsUtc, e.Outcome,
            p, IsPremium: p >= PremiumFrom);
    }
}
