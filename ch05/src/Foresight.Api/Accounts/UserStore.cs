namespace Foresight.Api.Accounts;

/// <summary>
/// The accounts as the rest of the app sees them. Creating, confirming and
/// signing in go through Identity's UserManager.
/// </summary>
public sealed class UserStore
{
    /// <summary>
    /// The form in which every table stores an email: trimmed and in lower
    /// case. A login token carries the email as typed.
    /// </summary>
    public static string Canonical(string email) =>
        email.Trim().ToLowerInvariant();
}
