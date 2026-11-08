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
    public DbSet<SubscriptionRow> Subscriptions => Set<SubscriptionRow>();
    public DbSet<ConsentRow> Consents => Set<ConsentRow>();
    public DbSet<ContractMailRow> ContractMails => Set<ContractMailRow>();
    public DbSet<MailThrottleRow> MailThrottle => Set<MailThrottleRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Identity's tables first: users, roles, claims, logins, tokens.
        base.OnModelCreating(model);

        model.Entity<FeedUsage>(e => e.HasKey(u => u.Month));

        model.Entity<SubscriptionRow>(e =>
        {
            // One row per PayPal subscription, so two events that arrive
            // together cannot insert it twice.
            e.HasIndex(s => s.PayPalId).IsUnique();
            // Every premium request looks the account up by this.
            e.HasIndex(s => s.Owner);
        });

        model.Entity<ConsentRow>(e => e.HasIndex(c => c.Owner));

        model.Entity<ContractMailRow>(e =>
        {
            // The claim and the webhook may report a new subscription at
            // the same moment. The key lets only one of them send the mail.
            e.HasKey(m => m.PayPalId);
            e.HasIndex(m => m.Owner);
        });

        // One row per address; the key is a hash of the address.
        model.Entity<MailThrottleRow>(e => e.HasKey(r => r.Key));
    }
}
