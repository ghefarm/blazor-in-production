namespace Foresight.Api.Data;

/// <summary>
/// The confirmation mails one address has had in its current round. A
/// round lasts one day from its first mail.
/// </summary>
public sealed class MailThrottleRow
{
    // A hash of the address, never the address: the row outlives a deleted
    // account on purpose, and it must not keep that account's email.
    public string Key { get; set; } = "";

    public int Count { get; set; }

    public DateTime FirstUtc { get; set; }

    public DateTime LastUtc { get; set; }
}
