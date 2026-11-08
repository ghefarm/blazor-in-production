namespace Foresight.Shared;

/// <summary>
/// A post the admin page shows before it is sent: the forecast's text for
/// its channel, which the admin may still edit.
/// </summary>
public sealed record ChannelDraft(
    string EventId, string Title, bool Premium, string Text);

/// <summary>A post the admin sends by hand, to one channel.</summary>
public sealed record ChannelPost(bool Premium, string Text);
