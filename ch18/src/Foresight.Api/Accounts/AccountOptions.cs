namespace Foresight.Api.Accounts;

/// <summary>The "Jwt" section: how login tokens are signed.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // Whoever holds the key can sign a token for any account. It comes only
    // from the host, and the API refuses to start with a short one.
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Foresight.Api";
    public string Audience { get; set; } = "Foresight.Web";
    public int Days { get; set; } = 7;
}

/// <summary>
/// The "LoginLockout" section: how many wrong passwords in a row lock an
/// account, and for how long. Identity keeps the count in the database, so
/// a restart does not hand out a fresh set of guesses.
/// </summary>
public sealed class LoginLockoutOptions
{
    public const string SectionName = "LoginLockout";

    public int MaxFailedAttempts { get; set; } = 3;
    public int LockSeconds { get; set; } = 15 * 60;
}
