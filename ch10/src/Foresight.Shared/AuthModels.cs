namespace Foresight.Shared;

/// <summary>A new account: the email is also the user name.</summary>
public sealed record RegisterRequest(string Email, string Password);

/// <summary>
/// A sign-in with email and password. With two-factor sign-in, the same
/// request is sent again with a code from the app or a recovery code.
/// </summary>
public sealed record LoginRequest(
    string Email,
    string Password,
    string? Code = null,
    string? RecoveryCode = null);

/// <summary>
/// The signed login token the website keeps for the visitor, or no token
/// and CodeNeeded when the account uses two-factor sign-in.
/// </summary>
public sealed record LoginResponse(string? Token, bool CodeNeeded = false);

/// <summary>The one password rule, shared by page and API.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    public static readonly string TooShortMessage =
        $"Your password needs at least {MinLength} characters.";
}
