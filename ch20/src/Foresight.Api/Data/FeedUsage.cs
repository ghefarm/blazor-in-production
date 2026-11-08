namespace Foresight.Api.Data;

/// <summary>How many feed requests one calendar month has used.</summary>
public sealed class FeedUsage
{
    /// <summary>The month, as "2026-11".</summary>
    public string Month { get; set; } = "";

    public int Requests { get; set; }
}
