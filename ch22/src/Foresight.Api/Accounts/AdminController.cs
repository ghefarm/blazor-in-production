using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Accounts;

/// <summary>
/// The admin's tools for owners who cannot help themselves: delete an
/// account on request, or switch two-factor sign-in off for someone who
/// lost the phone and the recovery codes. Check who is asking first, by a
/// mail to the account's address. Only the admin may call these.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AdminEmail.PolicyName)]
public sealed class AdminController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly UserManager<AppUser> _users;
    private readonly AccountDeletion _deletion;
    private readonly ISubscriptionStore _subscriptions;
    private readonly TimeProvider _clock;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IConfiguration config,
        UserManager<AppUser> users,
        AccountDeletion deletion,
        ISubscriptionStore subscriptions,
        TimeProvider clock,
        ILogger<AdminController> logger)
    {
        _config = config;
        _users = users;
        _deletion = deletion;
        _subscriptions = subscriptions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The page asks this first: a 403 means "not the admin".
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { Email = User.GetEmail() });

    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<AdminAccount>>> AccountsAsync(
        CancellationToken ct = default)
    {
        // Only these columns leave the database: no hash, no stamp, no key.
        var users = await _users.Users
            .Select(u => new { u.Email, u.EmailConfirmed, u.TwoFactorEnabled })
            .ToListAsync(ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        var accounts = new List<AdminAccount>();
        foreach (var u in users.OrderBy(u => u.Email, StringComparer.Ordinal))
        {
            var current = await _subscriptions.FindCurrentAsync(u.Email!, ct);
            accounts.Add(new AdminAccount(u.Email!, u.EmailConfirmed,
                u.TwoFactorEnabled, current?.PremiumUntil(now)));
        }
        return accounts;
    }

    [HttpPost("accounts/delete")]
    public async Task<IActionResult> DeleteAsync(
        AdminAccountRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
            return NotFound(new ErrorResponse("There is no such account."));

        // One click would lock the owner out of this page for good.
        if (_config.IsAdmin(user.Email))
            return BadRequest(new ErrorResponse(
                "The admin account cannot be deleted here."));

        // The same order as the owner's own delete (chapter 11).
        return await _deletion.DeleteAsync(user, by: "the admin", ct) switch
        {
            AccountDeletion.Result.Deleted => Ok(new AdminMessage(
                $"The account {user.Email} and its data are deleted.")),
            AccountDeletion.Result.PayPalFailed => StatusCode(502,
                new ErrorResponse("PayPal could not be told, so nothing "
                    + "was deleted. Try again in a few minutes.")),
            _ => StatusCode(500, new ErrorResponse(
                "The account could not be deleted. The log says why.")),
        };
    }

    [HttpPost("accounts/two-factor-off")]
    public async Task<IActionResult> TwoFactorOffAsync(
        AdminAccountRequest request)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
            return NotFound(new ErrorResponse("There is no such account."));
        if (!user.TwoFactorEnabled)
            return BadRequest(new ErrorResponse(
                "Two-factor sign-in is not on for this account."));

        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user);
        // Wrong codes may have locked the account too. Its owner starts
        // clean, with the password alone.
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);

        _logger.LogWarning("Two-factor sign-in switched off by the admin "
            + "for {Email}", user.Email);
        return Ok(new AdminMessage(
            $"Two-factor sign-in is off for {user.Email}."));
    }
}
