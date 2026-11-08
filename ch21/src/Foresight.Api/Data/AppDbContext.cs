using Foresight.Api.Accounts;
using Foresight.Api.Scheduling;
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
    public DbSet<VoteRow> Votes => Set<VoteRow>();
    public DbSet<RecordRow> TrackRecord => Set<RecordRow>();
    public DbSet<OutcomeRow> Outcomes => Set<OutcomeRow>();
    public DbSet<MailThrottleRow> MailThrottle => Set<MailThrottleRow>();
    public DbSet<SwitchRow> Switches => Set<SwitchRow>();
    public DbSet<ChannelLinkRow> ChannelLinks => Set<ChannelLinkRow>();

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

        // One vote per account and round. The database keeps the rule, so
        // two clicks at the same moment cannot store two votes.
        model.Entity<VoteRow>(e =>
        {
            e.HasIndex(v => new { v.Round, v.Voter }).IsUnique();
            // The deletion of an account looks its votes up by this.
            e.HasIndex(v => v.Voter);
        });

        // The track record: derived tables, built again by every refresh.
        model.Entity<RecordRow>(e =>
        {
            e.HasIndex(r => r.EventId).IsUnique();
            e.HasIndex(r => r.Feed);
        });
        model.Entity<OutcomeRow>(e => e.HasIndex(o => o.Feed));

        // One row per address; the key is a hash of the address.
        model.Entity<MailThrottleRow>(e => e.HasKey(r => r.Key));

        // The kill switch and any later on/off setting, by name.
        model.Entity<SwitchRow>(e => e.HasKey(s => s.Name));

        // One link per account, and one account per link: a join request
        // names only the link, and the link must lead back to one owner.
        model.Entity<ChannelLinkRow>(e =>
        {
            e.HasKey(l => l.Owner);
            e.HasIndex(l => l.Link).IsUnique();
        });
    }
}
