namespace Foresight.Telegram;

/// <summary>
/// The "Telegram" section, the same in the API and in the scheduler. The
/// token comes only from the host: whoever has it can post as the bot.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";

    // A public channel is named by its user name: "@foresight_free".
    public string FreeChannel { get; set; } = "";

    // A private channel has no name, only a number: "-100...".
    public string PremiumChannel { get; set; } = "";

    // Where the readers live. Start times in a message use this zone,
    // never the zone of the machine that sends it.
    public string TimeZone { get; set; } = "Europe/Berlin";

    public bool CanPost =>
        BotToken.Length > 0 && FreeChannel.Length > 0
        && PremiumChannel.Length > 0;

    /// <summary>The channel for one tier.</summary>
    public string ChannelFor(bool premium) =>
        premium ? PremiumChannel : FreeChannel;

    /// <summary>The readers' zone. Throws for a name the machine does
    /// not know, so check it at start with <see cref="HasKnownZone"/>.
    /// </summary>
    public TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

    public bool HasKnownZone =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _);
}
