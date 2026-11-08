using Foresight.Api.Accounts;
using Foresight.Api.Feeds;
using Foresight.Shared;
using Foresight.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Channels;

/// <summary>
/// Posting to the two Telegram channels by hand. Only the admin may, and
/// the check is here, whatever the page shows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AdminEmail.PolicyName)]
public sealed class ChannelsController : ControllerBase
{
    private readonly IEventSource _events;
    private readonly ITelegramSender _telegram;
    private readonly TelegramOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ChannelsController> _logger;

    public ChannelsController(
        IEventSource events,
        ITelegramSender telegram,
        IOptions<TelegramOptions> options,
        TimeProvider clock,
        ILogger<ChannelsController> logger)
    {
        _events = events;
        _telegram = telegram;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    // GET api/channels/drafts/weather?day=2026-11-02
    [HttpGet("drafts/{feed}")]
    public async Task<ActionResult<IReadOnlyList<ChannelDraft>>> DraftsAsync(
        string feed, [FromQuery] DateOnly? day, CancellationToken ct = default)
    {
        if (!_events.Feeds.Contains(feed))
            return NotFound(new ErrorResponse("There is no such feed."));

        var date = day ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        try
        {
            var events = await _events.GetEventsAsync(feed, date, ct);
            var zone = _options.Zone;
            return events.Select(Forecaster.For)
                .Select(f => new ChannelDraft(f.EventId, f.Title,
                    f.IsPremium, ForecastMessage.For(f, zone)))
                .ToList();
        }
        catch (FeedLimitException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("The feed is used up for this month."));
        }
    }

    [HttpPost("post")]
    public async Task<IActionResult> PostAsync(
        ChannelPost post, CancellationToken ct = default)
    {
        if (!_options.CanPost)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Telegram is not set up on this server."));
        if (string.IsNullOrWhiteSpace(post.Text))
            return BadRequest(new ErrorResponse("The post is empty."));

        // The tier picks the channel. The text may be edited; the channel
        // may not, so a premium text never reaches the public channel by
        // a slip of the mouse.
        var channel = _options.ChannelFor(post.Premium);
        try
        {
            await _telegram.SendAsync(channel, post.Text, ct);
        }
        catch (TelegramException ex)
        {
            _logger.LogWarning("A post by hand failed: {Reason}", ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway,
                new ErrorResponse(ex.Message));
        }

        _logger.LogInformation("{Admin} posted to the {Tier} channel",
            User.GetEmail(), post.Premium ? "premium" : "free");
        return Ok();
    }
}
