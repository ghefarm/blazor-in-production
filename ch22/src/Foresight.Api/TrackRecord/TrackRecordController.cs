using Foresight.Api.Accounts;
using Foresight.Api.Feeds;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.TrackRecord;

/// <summary>
/// The track record. Reading is public, because the page exists to show
/// it. A refresh reads files and rewrites two tables, so only the admin
/// may start one.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class TrackRecordController : ControllerBase
{
    private readonly TrackRecordService _record;
    private readonly IEventSource _events;

    public TrackRecordController(
        TrackRecordService record, IEventSource events)
    {
        _record = record;
        _events = events;
    }

    // GET api/trackrecord/weather
    [HttpGet("{feed}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrackRecordDto>> GetAsync(
        string feed, CancellationToken ct = default)
    {
        if (!_events.Feeds.Contains(feed))
            return NotFound(new ErrorResponse("There is no such feed."));
        return await _record.GetAsync(feed, ct);
    }

    [HttpPost("refresh")]
    [Authorize(Policy = AdminEmail.PolicyName)]
    public Task<RefreshReport> RefreshAsync(CancellationToken ct = default) =>
        _record.RefreshAsync(ct);
}
