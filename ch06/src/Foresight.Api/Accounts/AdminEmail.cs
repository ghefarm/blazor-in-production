using System.Security.Claims;

namespace Foresight.Api.Accounts;

/// <summary>
/// The one rule for "is this the admin": the configured AdminEmail. Nobody
/// is the admin while the setting is empty.
/// </summary>
public static class AdminEmail
{
    public static bool IsAdmin(this IConfiguration config, string? email) =>
        config["AdminEmail"] is { Length: > 0 } admin
        && email is { Length: > 0 }
        && UserStore.Canonical(email) == UserStore.Canonical(admin);

    /// <summary>The email a login token carries, or null.</summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email);
}
