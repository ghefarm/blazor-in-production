using Foresight.Api.Accounts;
using Foresight.Api.Defenses;
using Foresight.Api.Feeds;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Scheduling;

/// <summary>
/// What the scheduler asks for: every forecast, premium included, and the
/// kill switch, which the admin sets and the API keeps.
/// </summary>
[ApiController]
[Route("api/scheduler")]
public sealed class SchedulerController : ControllerBase
{
    private readonly SwitchStore _switches;
    private readonly TrustedCallers _callers;
    private readonly IEventSource _events;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(
        SwitchStore switches,
        TrustedCallers callers,
        IEventSource events,
        IConfiguration config,
        TimeProvider clock,
        ILogger<SchedulerController> logger)
    {
        _switches = switches;
        _callers = callers;
        _events = events;
        _config = config;
        _clock = clock;
        _logger = logger;
    }

    // GET api/scheduler/forecasts/weather?day=2026-11-02: every forecast of
    // the day, premium included. The key is the only way in: the scheduler
    // has no account, so there is no login to steal or to lock.
    [HttpGet("forecasts/{feed}")]
    public async Task<ActionResult<IReadOnlyList<ForecastDto>>> ForecastsAsync(
        string feed, [FromQuery] DateOnly? day, CancellationToken ct = default)
    {
        if (!_callers.IsScheduler(Request))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("Only the scheduler may ask."));
        if (!_events.Feeds.Contains(feed))
            return NotFound(new ErrorResponse("There is no such feed."));

        var date = day ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        try
        {
            var events = await _events.GetEventsAsync(feed, date, ct);
            return events.Select(Forecaster.For).ToList();
        }
        catch (FeedLimitException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("The feed is used up for this month."));
        }
    }

    // GET api/scheduler/posting: the scheduler asks with its key, the
    // admin page with the admin's token. Nobody else needs to know.
    [HttpGet("posting")]
    public async Task<ActionResult<PostingSwitch>> GetPostingAsync(
        CancellationToken ct = default)
    {
        if (!_callers.IsScheduler(Request) && !_config.IsAdmin(User.GetEmail()))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("Only the scheduler and the admin may ask."));

        return new PostingSwitch(
            await _switches.IsOnAsync(SwitchStore.Posting, ct));
    }

    [HttpPost("posting")]
    [Authorize(Policy = AdminEmail.PolicyName)]
    public async Task<ActionResult<PostingSwitch>> SetPostingAsync(
        PostingSwitch value, CancellationToken ct = default)
    {
        var admin = User.GetEmail()!;
        await _switches.SetAsync(SwitchStore.Posting, value.On, admin, ct);
        _logger.LogWarning("Posting switched {State} by {Admin}",
            value.On ? "on" : "off", admin);
        return value;
    }
}
