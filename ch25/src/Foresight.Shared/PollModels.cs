namespace Foresight.Shared;

/// <summary>
/// The "which feed next?" poll: the counts for everyone, and the caller's
/// own vote when the caller is signed in.
/// </summary>
/// <param name="Options">In the order of the settings, so a bar keeps its
/// place while the counts change.</param>
/// <param name="MyVote">The caller's candidate, or null.</param>
public sealed record PollDto(
    string Round,
    bool Open,
    int TotalVotes,
    IReadOnlyList<PollOptionDto> Options,
    string? MyVote);

/// <summary>One candidate and its votes.</summary>
public sealed record PollOptionDto(string Id, string Label, int Votes);

/// <summary>A vote for one candidate, by its id.</summary>
public sealed record VoteRequest(string Candidate);
