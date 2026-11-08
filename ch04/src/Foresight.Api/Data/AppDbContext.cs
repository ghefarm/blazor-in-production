using Foresight.Api.Accounts;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Data;

/// <summary>
/// The one database of Foresight. Every table lives here, so a new table is
/// a migration, never a new file.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // The accounts (Users) come from IdentityDbContext.
    public DbSet<FeedUsage> FeedUsage => Set<FeedUsage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Identity's tables first: users, roles, claims, logins, tokens.
        base.OnModelCreating(model);

        model.Entity<FeedUsage>(e => e.HasKey(u => u.Month));
    }
}
