namespace Foresight.Shared;

/// <summary>A subscriber's own join link to the premium channel.</summary>
public sealed record PremiumChannelLink(string Link);

/// <summary>What one sync of the premium channel did.</summary>
public sealed record ChannelSyncReport(
    int Approved, int Declined, int Removed);
