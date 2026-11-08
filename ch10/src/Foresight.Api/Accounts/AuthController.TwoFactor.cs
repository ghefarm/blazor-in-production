using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api.Accounts;

/// <summary>
/// Two-factor sign-in for the signed-in account: its status, switching it
/// on, switching it off. Not offered for the admin account: the owner
/// decided that it must always work with the password alone.
/// </summary>
public sealed partial class AuthController
{
    private const string NotForAdmin =
        "Two-factor sign-in is not offered for the admin account.";

    [HttpGet("2fa")]
    [Authorize]
    public Task<IActionResult> TwoFactorStatusAsync() =>
        ForSignedInUserAsync(async user => Ok(new TwoFactorStatus(
            Available: !_config.IsAdmin(user.Email),
            Enabled: user.TwoFactorEnabled,
            RecoveryCodesLeft: user.TwoFactorEnabled
                ? await _users.CountRecoveryCodesAsync(user)
                : 0)));

    [HttpPost("2fa/setup")]
    [Authorize]
    public Task<IActionResult> TwoFactorSetupAsync() =>
        ForSignedInUserAsync(async user =>
        {
            if (_config.IsAdmin(user.Email))
                return BadRequest(new ErrorResponse(NotForAdmin));
            if (user.TwoFactorEnabled)
                return BadRequest(new ErrorResponse(
                    "Two-factor sign-in is on already."));

            // A new key each time, so a key shown earlier (a screenshot,
            // an abandoned try) is worth nothing.
            await _users.ResetAuthenticatorKeyAsync(user);
            var key = await _users.GetAuthenticatorKeyAsync(user) ?? "";
            return Ok(new TwoFactorSetup(InGroups(key)));
        });

    [HttpPost("2fa/enable")]
    [Authorize]
    public Task<IActionResult> TwoFactorEnableAsync(TwoFactorCode request) =>
        ForSignedInUserAsync(async user =>
        {
            if (_config.IsAdmin(user.Email))
                return BadRequest(new ErrorResponse(NotForAdmin));

            // On only after the app has made a right code. A key typed in
            // wrongly would otherwise lock the owner out at the next login.
            var code = (request.Code ?? "").Replace(" ", "");
            if (code.Length == 0 || !await _users.VerifyTwoFactorTokenAsync(
                    user, _users.Options.Tokens.AuthenticatorTokenProvider,
                    code))
                return BadRequest(new ErrorResponse("That code is not "
                    + "right. Check the time on your phone and type the "
                    + "newest code."));

            await _users.SetTwoFactorEnabledAsync(user, true);
            var codes =
                await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)
                ?? [];
            return Ok(new RecoveryCodes(codes.ToList()));
        });

    [HttpPost("2fa/disable")]
    [Authorize]
    public Task<IActionResult> TwoFactorDisableAsync(PasswordRequest request) =>
        ForSignedInUserAsync(async user =>
        {
            // The password again: a token from a session that someone left
            // open must not be enough. It counts like any wrong password;
            // 400 and not 401, because the session itself is fine.
            if (!await _users.CheckPasswordAsync(user, request.Password ?? ""))
                return await CountFailureAsync(user)
                    ?? BadRequest(new ErrorResponse("Wrong password."));

            await _users.SetTwoFactorEnabledAsync(user, false);
            // The old key is worth nothing from now on.
            await _users.ResetAuthenticatorKeyAsync(user);
            return Ok(new TwoFactorStatus(true, false, 0));
        });

    // Runs the action for the account the token names, or answers 401 when
    // that account no longer exists.
    private async Task<IActionResult> ForSignedInUserAsync(
        Func<AppUser, Task<IActionResult>> action) =>
        User.GetEmail() is { } email
        && await _users.FindByEmailAsync(email) is { } user
            ? await action(user)
            : Unauthorized(new ErrorResponse("Please sign in again."));

    // "abcd efgh ijkl ...": easier to type into a phone than one long text.
    private static string InGroups(string key) => string.Join(' ',
        key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
}
