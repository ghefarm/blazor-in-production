using Foresight.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Feeds;

/// <summary>The forecasts of one feed on one day.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ForecastsController : ControllerBase
{
    private readonly IEventSource _events;
    private readonly TimeProvider _clock;

    public ForecastsController(IEventSource events, TimeProvider clock)
    {
        _events = events;
        _clock = clock;
    }

    // GET api/forecasts/weather?day=2026-11-02
    [HttpGet("{feed}")]
    public async Task<ActionResult<IReadOnlyList<ForecastDto>>> GetAsync(
        string feed, [FromQuery] DateOnly? day, CancellationToken ct = default)
    {
        if (!_events.Feeds.Contains(feed))
            return NotFound(new ErrorResponse("There is no such feed."));

        var date = day ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        try
        {
            var events = await _events.GetEventsAsync(feed, date, ct);

            // Nobody has premium yet, so the premium forecasts stay out.
            return events.Select(Forecaster.For)
                .Where(f => !f.IsPremium)
                .ToList();
        }
        catch (FeedLimitException)
        {
            return Unavailable();
        }
    }

    /// <summary>The answer when the feed has nothing left for a caller.
    /// </summary>
    internal static ObjectResult Unavailable() =>
        new(new ErrorResponse(
            "Forecasts are not available now. Please try later."))
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
        };
}
