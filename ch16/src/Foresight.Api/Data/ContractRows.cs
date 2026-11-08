namespace Foresight.Api.Data;

/// <summary>A consent given before a payment, with its words.</summary>
public sealed class ConsentRow
{
    public int Id { get; set; }

    // The account, in the form UserStore.Canonical gives an email.
    public string Owner { get; set; } = "";

    public string Version { get; set; } = "";

    // The server's own text of that version, never one a browser sent.
    public string Text { get; set; } = "";

    public DateTime ConsentedUtc { get; set; }
}

/// <summary>A sent contract mail: at most one per subscription.</summary>
public sealed class ContractMailRow
{
    // The key: a second row for the same subscription cannot exist.
    public string PayPalId { get; set; } = "";

    public string Owner { get; set; } = "";

    public DateTime SentUtc { get; set; }
}
