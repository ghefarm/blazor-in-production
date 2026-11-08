using Foresight.Api.Accounts;
using Foresight.Api.Defenses;
using Foresight.Shared;
using Foresight.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Channels;

/// <summary>
/// The premium channel's door: a subscriber asks for their own link, and
/// the scheduler asks for the sync on every tick.
/// </summary>
[ApiController]
[Route("api/premium-channel")]
public sealed class PremiumChannelController : ControllerBase
{
    private const string TelegramDown =
        "Telegram could not be reached just now. Please try again in a few "
        + "minutes.";

    private readonly ChannelMembership _members;
    private readonly TrustedCallers _callers;
    private readonly ILogger<PremiumChannelController> _logger;

    public PremiumChannelController(
        ChannelMembership members,
        TrustedCallers callers,
        ILogger<PremiumChannelController> logger)
    {
        _members = members;
        _callers = callers;
        _logger = logger;
    }

    // GET api/premium-channel/link: the signed-in account's own link. Only
    // for premium now; the bot asks again when the request to join comes.
    [HttpGet("link")]
    [Authorize]
    public async Task<ActionResult<PremiumChannelLink>> LinkAsync(
        CancellationToken ct = default)
    {
        (LinkOutcome Outcome, string? Link) result;
        try
        {
            result = await _members.LinkForAsync(User.GetEmail()!, ct);
        }
        catch (TelegramException ex)
        {
            _logger.LogWarning("No channel link made: {Why}", ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway,
                new ErrorResponse(TelegramDown));
        }

        return result.Outcome switch
        {
            LinkOutcome.Ready => new PremiumChannelLink(result.Link!),
            LinkOutcome.NotConfigured => StatusCode(
                StatusCodes.Status503ServiceUnavailable, new ErrorResponse(
                    "The premium channel is not open yet.")),
            _ => NotFound(new ErrorResponse(
                "The premium channel is for accounts with premium.")),
        };
    }

    // POST api/premium-channel/sync: the scheduler's key only. Anyone else
    // could make the bot work, and learn from the answer how many left.
    [HttpPost("sync")]
    public async Task<ActionResult<ChannelSyncReport>> SyncAsync(
        CancellationToken ct = default)
    {
        if (!_callers.IsScheduler(Request))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("Only the scheduler may ask."));
        try
        {
            return await _members.SyncAsync(ct);
        }
        catch (TelegramException ex)
        {
            _logger.LogWarning("The premium channel sync failed: {Why}",
                ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway,
                new ErrorResponse(ex.Message));
        }
    }
}
