using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Data;

/// <summary>
/// The one database of Foresight. Every table lives here, so a new table is
/// a migration, never a new file.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<FeedUsage> FeedUsage => Set<FeedUsage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<FeedUsage>(e => e.HasKey(u => u.Month));
    }
}
