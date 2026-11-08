namespace Foresight.Api.Data;

/// <summary>One account's vote in one round of the poll.</summary>
public sealed class VoteRow
{
    public int Id { get; set; }

    public string Round { get; set; } = "";

    // The account, in the form UserStore.Canonical gives an email. The
    // deletion of the account finds its votes by this.
    public string Voter { get; set; } = "";

    // A PollCandidate.Id.
    public string Candidate { get; set; } = "";

    public DateTime CastUtc { get; set; }
}
