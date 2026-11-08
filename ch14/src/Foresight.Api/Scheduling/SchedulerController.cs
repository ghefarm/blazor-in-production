using Foresight.Api.Accounts;
using Foresight.Api.Defenses;
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
    private readonly IConfiguration _config;
    private readonly ILogger<SchedulerController> _logger;

    public SchedulerController(
        SwitchStore switches,
        TrustedCallers callers,
        IConfiguration config,
        ILogger<SchedulerController> logger)
    {
        _switches = switches;
        _callers = callers;
        _config = config;
        _logger = logger;
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
