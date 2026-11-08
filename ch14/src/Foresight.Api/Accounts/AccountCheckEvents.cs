using Foresight.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Foresight.Api.Accounts;

/// <summary>
/// Checks the account behind every login token. A token is valid for
/// seven days, and its signature stays valid that long, also after its
/// account is deleted. This check makes the API notice.
/// </summary>
public sealed class AccountCheckEvents : JwtBearerEvents
{
    private readonly UserStore _users;

    public AccountCheckEvents(UserStore users)
    {
        _users = users;
    }

    // Runs for every request with a token, after JwtBearer's own checks
    // passed: signature, issuer, audience, date. A token without an
    // account counts as no token: [Authorize] answers 401, and an open
    // endpoint sees a stranger.
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var email = context.Principal?.GetEmail();
        if (email is null || !await _users.ExistsAsync(
                email, context.HttpContext.RequestAborted))
            context.Fail("The account of this token no longer exists.");
    }

    // A 401 carries a sentence, like every other error of the API, so the
    // page has something to show when it signs the visitor out.
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return context.Response.WriteAsJsonAsync(new ErrorResponse(
                context.AuthenticateFailure is null
                    ? "Please sign in."
                    : "Please sign in again."),
            context.HttpContext.RequestAborted);
    }
}
