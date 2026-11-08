namespace Foresight.Api.Data;

/// <summary>A subscriber's own way into the premium channel.</summary>
public sealed class ChannelLinkRow
{
    // The account, in the one form every table uses. It is the key, so an
    // account can never hold two links.
    public string Owner { get; set; } = "";

    // The join link Telegram made for this account alone.
    public string Link { get; set; } = "";

    // The Telegram user the bot let in with this link. The first one is
    // the only one; whoever comes with a forwarded copy is turned away.
    public long? TelegramUserId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
