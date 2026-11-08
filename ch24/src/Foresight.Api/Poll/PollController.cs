using Foresight.Api.Accounts;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Poll;

/// <summary>
/// The "which feed next?" poll. Anyone may read the result. Voting needs
/// an account: one vote per account, and a later vote replaces the first.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PollController : ControllerBase
{
    private readonly PollStore _poll;

    public PollController(PollStore poll)
    {
        _poll = poll;
    }

    // Anonymous, but a valid token is still read, so a signed-in caller
    // also gets their own vote back.
    [HttpGet]
    [AllowAnonymous]
    public Task<PollDto> GetAsync(CancellationToken ct = default) =>
        _poll.GetAsync(User.GetEmail(), ct);

    // [Authorize] lets in only a token whose account still exists: the
    // check of chapter 7 runs before this method.
    [HttpPut("vote")]
    [Authorize]
    public async Task<IActionResult> VoteAsync(
        VoteRequest request, CancellationToken ct = default)
    {
        if (!_poll.IsOpen)
            return Conflict(new ErrorResponse("Voting is closed."));
        if (!_poll.IsCandidate(request.Candidate))
            return BadRequest(new ErrorResponse("There is no such feed."));

        var voter = User.GetEmail()!;
        await _poll.VoteAsync(voter, request.Candidate, ct);
        return Ok(await _poll.GetAsync(voter, ct));
    }
}
