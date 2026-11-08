using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Accounts;

/// <summary>
/// Identity's password hasher, plus the format of the first, hand-built
/// login: "pbkdf2$iterations$salt$hash" (PBKDF2 with SHA-256, Base64). An
/// account from that time keeps its password. Its first sign-in answers
/// SuccessRehashNeeded, and Identity stores a hash in its own format, so
/// the old format disappears one account at a time.
/// </summary>
public sealed class LegacyPasswordHasher : IPasswordHasher<AppUser>
{
    private const string LegacyPrefix = "pbkdf2$";

    private readonly PasswordHasher<AppUser> _current;

    public LegacyPasswordHasher(IOptions<PasswordHasherOptions> options)
    {
        _current = new PasswordHasher<AppUser>(options);
    }

    public string HashPassword(AppUser user, string password) =>
        _current.HashPassword(user, password);

    public PasswordVerificationResult VerifyHashedPassword(
        AppUser user, string hashedPassword, string providedPassword)
    {
        if (!hashedPassword.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            return _current.VerifyHashedPassword(
                user, hashedPassword, providedPassword);

        return VerifyLegacy(hashedPassword, providedPassword)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Failed;
    }

    // The old check: iterations and salt come from the stored value, and the
    // comparison takes the same time whether the password is right or not.
    private static bool VerifyLegacy(string encoded, string password)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4
            || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        if (expected.Length == 0)
            return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
            HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
