namespace Foresight.Api.Poll;

/// <summary>
/// The "Poll" section of the settings. The candidates live here and not in
/// code, because they change: a feed that goes live leaves the list, and
/// the next poll gets a new round.
/// </summary>
public sealed class PollOptions
{
    public const string SectionName = "Poll";

    // Names the poll. Votes are kept per round, so a new round starts from
    // zero, and the votes of the old one stay out of it.
    public string Round { get; set; } = "";

    // False keeps the result on the page and refuses new votes.
    public bool Open { get; set; }

    public List<PollCandidate> Candidates { get; set; } = [];
}

/// <summary>One feed the visitors can vote for.</summary>
public sealed class PollCandidate
{
    // Stored with every vote, so it must not change while a round runs.
    public string Id { get; set; } = "";

    public string Label { get; set; } = "";
}
