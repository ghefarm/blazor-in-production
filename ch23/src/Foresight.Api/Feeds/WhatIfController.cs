using Foresight.Api.Accounts;
using Foresight.Api.Defenses;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Foresight.Api.Feeds;

/// <summary>
/// The forecast for one event at a number the caller chooses. A subscriber
/// gets the forecast; everyone else gets its direction and a hint.
/// </summary>
[ApiController]
[Route("api/what-if")]
public sealed class WhatIfController : ControllerBase
{
    private readonly IEventSource _events;
    private readonly PremiumAccess _premium;
    private readonly FeedReserve _reserve;
    private readonly TimeProvider _clock;

    public WhatIfController(IEventSource events, PremiumAccess premium,
        FeedReserve reserve, TimeProvider clock)
    {
        _events = events;
        _premium = premium;
        _reserve = reserve;
        _clock = clock;
    }

    // POST api/what-if  { "feed": "weather", "eventId": "...", "score": 70 }
    [HttpPost]
    [EnableRateLimiting(VisitorLimits.WhatIf)]
    public async Task<ActionResult<WhatIfDto>> AskAsync(
        WhatIfRequest request, CancellationToken ct = default)
    {
        if (!_events.Feeds.Contains(request.Feed))
            return NotFound(new ErrorResponse("There is no such feed."));
        if (request.Score is < 0 or > 100)
            return BadRequest(new ErrorResponse(
                "The number must be from 0 to 100."));
        if (await _reserve.IsClosedToAsync(Request, ct))
            return ForecastsController.Unavailable();

        FeedEvent? real;
        try
        {
            real = await TodaysEventAsync(request, ct);
        }
        catch (FeedLimitException)
        {
            return ForecastsController.Unavailable();
        }

        var email = User.GetEmail();
        var score = request.Score;
        if (email is null)
        {
            // Without a login: only an event of today's feed, at the feed's
            // own number. The number sent along is not used, so there is
            // nothing to make up.
            if (real is null)
                return Unauthorized(new ErrorResponse(
                    "Please sign in to ask about numbers of your own."));
            score = real.Score;
        }

        var asked = Forecaster.For((real ?? Unknown(request)) with
        {
            Score = score,
        });
        var direction = DirectionOf(asked.Probability);
        if (await _premium.HasAsync(email, ct))
            return new WhatIfDto(direction, asked.Probability, 0);

        // The hint about premium forecasts comes in chapter 24. Until then
        // it is null: not worked out.
        return new WhatIfDto(direction, Probability: null, HiddenPremium: null);
    }

    // The free part of the answer. Its two cuts are far from the premium
    // cut, so "likely" looks the same just below premium and inside it.
    private static string DirectionOf(double probability) => probability switch
    {
        >= 0.55 => "likely",
        <= 0.45 => "unlikely",
        _ => "open",
    };

    // The event as today's feed lists it, found by its id and never by
    // the caller's numbers. Null when the feed has no such event today.
    private async Task<FeedEvent?> TodaysEventAsync(
        WhatIfRequest request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var events = await _events.GetEventsAsync(request.Feed, today, ct);
        return events.FirstOrDefault(e => e.Id == request.EventId);
    }

    // An event the feed does not list today: only the caller's number is
    // known, and that is all the forecast needs.
    private static FeedEvent Unknown(WhatIfRequest request) =>
        new(request.EventId, request.Feed, "", "", DateTime.MinValue,
            request.Score);
}
