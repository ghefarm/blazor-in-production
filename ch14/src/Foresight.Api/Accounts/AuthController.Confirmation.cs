using Foresight.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Accounts;

/// <summary>
/// The confirmation mail: sent at registration, sent again on request, and
/// never more often than the throttle allows.
/// </summary>
public sealed partial class AuthController
{
    /// <summary>Sends the confirmation link again, with no password.</summary>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmationAsync(
        ResendConfirmationRequest request, CancellationToken ct = default)
    {
        // An unknown or a confirmed address gets no mail and the same
        // answer as a mail that went out.
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null || user.EmailConfirmed)
            return Ok();

        if (await MailConfirmationAsync(user, ct) is { } wait)
        {
            Response.Headers.RetryAfter =
                ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ErrorResponse("We sent a link to this address a short "
                    + "while ago. Please look in your inbox and your spam "
                    + $"folder, or try again in {Readable(wait)}."));
        }
        return Ok();
    }

    // Every confirmation mail goes out through here, so none can skip the
    // throttle. The count belongs to the address, not to the account, so
    // an account that is deleted and registered again starts no new round.
    // The throttle is asked before a new link is made: a refused try costs
    // nothing and leaves the links already sent as they are.
    private async Task<TimeSpan?> MailConfirmationAsync(
        AppUser user, CancellationToken ct)
    {
        if (await _throttle.TryTakeAsync(user.Email!, ct) is { } wait)
            return wait;

        try
        {
            await SendConfirmationAsync(user, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The account exists either way, and its owner can ask again
            // when the turn is over. The log says why no mail went out.
            _logger.LogError(ex, "Could not send a confirmation mail");
        }
        return null;
    }

    // A positive wait, rounded up: "1 minute", "10 minutes", "24 hours".
    private static string Readable(TimeSpan wait)
    {
        var (count, unit) = wait.TotalMinutes > 90
            ? ((int)Math.Ceiling(wait.TotalHours), "hour")
            : ((int)Math.Ceiling(wait.TotalMinutes), "minute");
        return $"{count} {unit}{(count == 1 ? "" : "s")}";
    }
}
