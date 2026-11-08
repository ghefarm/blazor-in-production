using Microsoft.AspNetCore.Identity;

namespace Foresight.Api.Accounts;

/// <summary>
/// The lookup form of a name or an email: trimmed and in upper case, so
/// " Ann@Example.com" finds the account "ann@example.com". Registered once,
/// before Identity, so Identity and our own stores look an account up the
/// same way.
/// </summary>
public sealed class EmailLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name) =>
        name?.Trim().Normalize().ToUpperInvariant();

    public string? NormalizeEmail(string? email) => NormalizeName(email);
}
