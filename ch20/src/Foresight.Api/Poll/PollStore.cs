using Foresight.Api.Accounts;
using Foresight.Api.Data;
using Foresight.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Poll;

/// <summary>The votes of the poll, kept per round in the database.</summary>
public sealed class PollStore
{
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly PollOptions _options;
    private readonly TimeProvider _clock;

    public PollStore(
        IDbContextFactory<AppDbContext> db,
        IOptions<PollOptions> options,
        TimeProvider clock)
    {
        _db = db;
        _options = options.Value;
        _clock = clock;
    }

    public bool IsOpen => _options.Open && _options.Candidates.Count > 0;

    public bool IsCandidate(string? id) =>
        _options.Candidates.Any(c => c.Id == id);

    /// <summary>The counts, and the caller's vote if there is one.</summary>
    public async Task<PollDto> GetAsync(
        string? voter, CancellationToken ct = default)
    {
        var round = _options.Round;
        await using var db = await _db.CreateDbContextAsync(ct);
        var counts = await db.Votes.Where(v => v.Round == round)
            .GroupBy(v => v.Candidate)
            .Select(g => new { g.Key, Votes = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Votes, ct);

        // Only the candidates in the settings are listed. A vote for one
        // that was taken out is neither shown nor counted.
        var options = _options.Candidates
            .Select(c => new PollOptionDto(
                c.Id, c.Label, counts.GetValueOrDefault(c.Id)))
            .ToList();

        string? mine = null;
        if (voter is not null)
        {
            var key = UserStore.Canonical(voter);
            mine = await db.Votes
                .Where(v => v.Round == round && v.Voter == key)
                .Select(v => v.Candidate)
                .FirstOrDefaultAsync(ct);
        }
        return new PollDto(round, IsOpen, options.Sum(o => o.Votes),
            options, IsCandidate(mine) ? mine : null);
    }

    /// <summary>
    /// Stores the account's vote, or moves its vote of this round to the
    /// new candidate.
    /// </summary>
    public async Task VoteAsync(
        string voter, string candidate, CancellationToken ct = default)
    {
        var round = _options.Round;
        var key = UserStore.Canonical(voter);
        var now = _clock.GetUtcNow().UtcDateTime;
        await using var db = await _db.CreateDbContextAsync(ct);

        // A change of mind: the account has a row, so move it.
        if (await MoveAsync() > 0)
            return;

        db.Votes.Add(new VoteRow
        {
            Round = round, Voter = key, Candidate = candidate, CastUtc = now,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request of the same account (a double click) was
            // first, and the unique index refused this row. Move that one.
            // With no row at all, the insert failed for another reason,
            // and that must not be reported as a stored vote.
            if (await MoveAsync() == 0)
                throw;
        }

        Task<int> MoveAsync() => db.Votes
            .Where(v => v.Round == round && v.Voter == key)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Candidate, candidate)
                .SetProperty(v => v.CastUtc, now), ct);
    }

    /// <summary>Removes the account's votes in every round. Safe to
    /// repeat.</summary>
    public async Task ForgetAsync(
        string voter, CancellationToken ct = default)
    {
        var key = UserStore.Canonical(voter);
        await using var db = await _db.CreateDbContextAsync(ct);
        await db.Votes.Where(v => v.Voter == key).ExecuteDeleteAsync(ct);
    }
}
