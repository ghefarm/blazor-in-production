using System.Net;
using System.Net.Mail;
using System.Text;
using Foresight.Api.Mail;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Foresight.Api.Accounts;

/// <summary>Accounts: register, confirm the email, sign in.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed partial class AuthController : ControllerBase
{
    // The same answer for an unknown email and a wrong password, so the
    // login does not tell a stranger which emails have an account.
    private static readonly ErrorResponse WrongPassword =
        new("Wrong email or password.");

    private static readonly ErrorResponse WrongCode =
        new("That code is not right.");

    private readonly UserManager<AppUser> _users;
    private readonly TokenIssuer _tokens;
    private readonly IMailSender _mail;
    private readonly IConfiguration _config;

    public AuthController(
        UserManager<AppUser> users,
        TokenIssuer tokens,
        IMailSender mail,
        IConfiguration config)
    {
        _users = users;
        _tokens = tokens;
        _mail = mail;
        _config = config;
    }

    /// <summary>The signed-in account, as the token names it.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new { Email = User.GetEmail() });

    private enum SecondFactor { Passed, Missing, Wrong }

    // An account with two-factor sign-in needs a code from its app, or one
    // of its recovery codes, as well as the password.
    private async Task<SecondFactor> CheckSecondFactorAsync(
        AppUser user, LoginRequest request)
    {
        if (!user.TwoFactorEnabled)
            return SecondFactor.Passed;

        // The app shows "123 456"; people type it with a space or a dash.
        var code = (request.Code ?? "").Replace(" ", "").Replace("-", "");
        if (code.Length > 0)
            return await _users.VerifyTwoFactorTokenAsync(user,
                    _users.Options.Tokens.AuthenticatorTokenProvider, code)
                ? SecondFactor.Passed
                : SecondFactor.Wrong;

        // A recovery code has a dash of its own, so only spaces go.
        var recovery = (request.RecoveryCode ?? "").Replace(" ", "");
        if (recovery.Length > 0)
            return (await _users.RedeemTwoFactorRecoveryCodeAsync(
                    user, recovery)).Succeeded
                ? SecondFactor.Passed
                : SecondFactor.Wrong;

        return SecondFactor.Missing;
    }

    // The password check of every endpoint that has one, with its lock.
    // The lock is looked at before the password, so a right guess during
    // the lock does not work.
    private async Task<(AppUser? User, IActionResult? Denied)>
        CheckPasswordAsync(LoginRequest request)
    {
        var user = await _users.FindByEmailAsync(request.Email ?? "");
        if (user is null)
            return (null, Unauthorized(WrongPassword));

        if (!_config.IsAdmin(user.Email)
            && await _users.IsLockedOutAsync(user))
            return (null, await LockedAsync(user));

        if (!await _users.CheckPasswordAsync(user, request.Password ?? ""))
            return (null, await CountFailureAsync(user)
                ?? Unauthorized(WrongPassword));
        return (user, null);
    }

    // Counts a wrong password or code towards the lock, and returns the
    // answer for a lock that starts now. The admin is never counted, so a
    // stranger cannot lock the owner out by guessing on purpose; a long
    // password carries that account.
    private async Task<IActionResult?> CountFailureAsync(AppUser user)
    {
        if (_config.IsAdmin(user.Email))
            return null;
        await _users.AccessFailedAsync(user);
        return await _users.IsLockedOutAsync(user)
            ? await LockedAsync(user)
            : null;
    }

    private async Task<IActionResult> LockedAsync(AppUser user)
    {
        var until = await _users.GetLockoutEndDateAsync(user);
        var minutes = until is { } end
            ? Math.Max(1, (int)Math.Ceiling(
                (end - DateTimeOffset.UtcNow).TotalMinutes))
            : 1;
        return StatusCode(StatusCodes.Status429TooManyRequests,
            new ErrorResponse("Too many wrong attempts. Try again in "
                + $"{minutes} minute{(minutes == 1 ? "" : "s")}."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginRequest request)
    {
        var (user, denied) = await CheckPasswordAsync(request);
        if (user is null)
            return denied!;

        // Asked after the password, so an unconfirmed account is only
        // revealed to someone who knows its password.
        if (!user.EmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("Please confirm your email first."));

        switch (await CheckSecondFactorAsync(user, request))
        {
            case SecondFactor.Missing:
                // The password was right, but there is no token yet. The
                // page asks for the code and sends the request again.
                return Ok(new LoginResponse(null, CodeNeeded: true));
            case SecondFactor.Wrong:
                return await CountFailureAsync(user)
                    ?? Unauthorized(WrongCode);
        }

        // Cleared only now, after the whole sign-in: a right password
        // followed by wrong codes still runs into the lock.
        await _users.ResetAccessFailedCountAsync(user);
        return Ok(new LoginResponse(_tokens.IssueFor(user.Email!)));
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        if (!IsEmail(request.Email))
            return BadRequest(new ErrorResponse("Please enter a valid email."));
        if (request.Password is not { Length: >= PasswordRules.MinLength })
            return BadRequest(new ErrorResponse(PasswordRules.TooShortMessage));

        var email = UserStore.Canonical(request.Email);
        var user = new AppUser { UserName = email, Email = email };
        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return created.Errors.Any(e => e.Code
                    is nameof(IdentityErrorDescriber.DuplicateEmail)
                    or nameof(IdentityErrorDescriber.DuplicateUserName))
                ? Conflict(new ErrorResponse(
                    "An account with this email exists already."))
                : BadRequest(new ErrorResponse(
                    created.Errors.First().Description));

        await SendConfirmationAsync(user, ct);
        return Ok();
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmailAsync(
        [FromQuery] string email, [FromQuery] string token)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
            return BadRequest(new ErrorResponse("This link is not valid."));

        // A second click on the same link is not an error.
        if (user.EmailConfirmed)
            return Ok();

        var confirmed = Decode(token) is { } raw
            && (await _users.ConfirmEmailAsync(user, raw)).Succeeded;
        return confirmed
            ? Ok()
            : BadRequest(new ErrorResponse(
                "This link is not valid or has expired."));
    }

    private async Task SendConfirmationAsync(
        AppUser user, CancellationToken ct)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var encoded =
            WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = $"{_config["FrontendUrl"]}/confirm-email"
            + $"?email={WebUtility.UrlEncode(user.Email)}&token={encoded}";

        // The link also as text: some mail programs remove a link they do
        // not trust, and the reader is then left with nothing to click.
        var shown = WebUtility.HtmlEncode(link);
        await _mail.SendAsync(user.Email!, "Confirm your Foresight account",
            $"<p><a href=\"{shown}\">Confirm your email</a></p>"
            + $"<p>If the link does not work, copy this address:<br>"
            + $"{shown}</p>", ct);
    }

    private static string? Decode(string? token)
    {
        try
        {
            return token is null
                ? null
                : Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsEmail(string? email) =>
        email is { Length: > 3 and <= 254 }
        && MailAddress.TryCreate(email.Trim(), out var parsed)
        && parsed.Address == email.Trim();
}
