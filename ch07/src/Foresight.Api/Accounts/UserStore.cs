using Foresight.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Accounts;

/// <summary>
/// Answers "does this account exist?" for the code that only has an email.
/// Creating, confirming and signing in go through Identity's UserManager.
/// </summary>
public sealed class UserStore
{
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly ILookupNormalizer _normalizer;

    public UserStore(
        IDbContextFactory<AppDbContext> db, ILookupNormalizer normalizer)
    {
        _db = db;
        _normalizer = normalizer;
    }

    public async Task<bool> ExistsAsync(
        string email, CancellationToken ct = default)
    {
        // The same lookup form as UserManager.FindByEmailAsync.
        var key = _normalizer.NormalizeEmail(email);
        await using var db = await _db.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(u => u.NormalizedEmail == key, ct);
    }

    /// <summary>
    /// The form in which every table stores an email: trimmed and in lower
    /// case. A login token carries the email as typed.
    /// </summary>
    public static string Canonical(string email) =>
        email.Trim().ToLowerInvariant();
}
