using Foresight.Api.Data;
using Foresight.Shared;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.TrackRecord;

/// <summary>
/// Builds the track record from what was posted and what happened, and
/// serves it per feed. Nothing here makes a forecast.
/// </summary>
public sealed class TrackRecordService
{
    // One refresh at a time: a second one would delete what the first writes.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly IPostedForecasts _posted;
    private readonly IOutcomeSource _outcomes;
    private readonly TimeProvider _clock;

    public TrackRecordService(
        IDbContextFactory<AppDbContext> db,
        IPostedForecasts posted,
        IOutcomeSource outcomes,
        TimeProvider clock)
    {
        _db = db;
        _posted = posted;
        _outcomes = outcomes;
        _clock = clock;
    }

    /// <summary>
    /// Builds both tables again from their sources. Nothing in them is the
    /// only copy of anything, so a fix in the scoring is one refresh away.
    /// </summary>
    public async Task<RefreshReport> RefreshAsync(
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // What really went out, read back. Forecasting the same events
            // again today would score today's rules, not the posted ones.
            var records = (await _posted.ReadAsync(ct))
                .Where(SaysYes)
                .DistinctBy(f => f.EventId)
                .Select(ToRow)
                .ToList();
            var outcomes = await OutcomesAsync(records, ct);
            foreach (var record in records)
                record.Happened =
                    OutcomeFinder.Find(record, outcomes)?.Happened;

            await SaveAsync(records, outcomes, ct);
            return Report(records, outcomes);
        }
        finally
        {
            _gate.Release();
        }
    }

    // 72 percent for rain is a forecast of rain. 30 percent for rain is a
    // forecast that it stays dry, and 50 is no forecast at all. Scoring the
    // last two as forecasts of rain would count the opposite of what they
    // said, so the record keeps only the forecasts that said yes.
    private static bool SaysYes(ForecastDto f) => f.Probability > 0.5;

    /// <summary>The record of one feed, as anyone may see it.</summary>
    public async Task<TrackRecordDto> GetAsync(
        string feed, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);

        // Public, so a premium forecast waits for its result. Before it,
        // the record would hand out the product while it is still useful.
        var rows = await db.TrackRecord.AsNoTracking()
            .Where(r => r.Feed == feed && (!r.IsPremium || r.Happened != null))
            .OrderBy(r => r.StartsUtc)
            .ToListAsync(ct);

        // The baseline: every event of the period, forecast or not.
        var events = db.Outcomes.Where(o => o.Feed == feed);
        return new TrackRecordDto(feed,
            await events.CountAsync(ct),
            await events.CountAsync(o => o.Happened, ct),
            rows.Select(ToDto).ToList());
    }

    // From the day before the first forecast, so an event late in the
    // evening still finds its outcome on the source's next day.
    private async Task<List<RecordedOutcome>> OutcomesAsync(
        List<RecordRow> records, CancellationToken ct)
    {
        var all = new List<RecordedOutcome>();
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        foreach (var feed in records.GroupBy(r => r.Feed))
        {
            var first = DateOnly.FromDateTime(feed.Min(r => r.StartsUtc));
            all.AddRange(await _outcomes.GetAsync(
                feed.Key, first.AddDays(-1), today, ct));
        }
        return all;
    }

    private async Task SaveAsync(List<RecordRow> records,
        List<RecordedOutcome> outcomes, CancellationToken ct)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct);
        await db.TrackRecord.ExecuteDeleteAsync(ct);
        await db.Outcomes.ExecuteDeleteAsync(ct);
        db.TrackRecord.AddRange(records);
        db.Outcomes.AddRange(outcomes.Select(o => new OutcomeRow
        {
            Feed = o.Feed, Name = o.Name,
            StartsLocal = o.StartsLocal, Happened = o.Happened,
        }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    // Open is "not out yet". Missing is "out, but no outcome fits it":
    // the source has reported a later event of the feed, so it should know
    // this one. Missing forecasts are named, so someone looks at them.
    private static RefreshReport Report(
        List<RecordRow> records, List<RecordedOutcome> outcomes)
    {
        var open = records.Where(r => r.Happened is null).ToList();
        var missing = open
            .Where(r => outcomes.Any(o => o.Feed == r.Feed
                && o.StartsLocal > r.StartsUtc + OutcomeFinder.Window))
            .Select(r => $"{r.Feed}: {r.Title}, {r.StartsUtc:u}")
            .ToList();
        return new RefreshReport(records.Count,
            records.Count - open.Count, open.Count, missing);
    }

    private static RecordRow ToRow(ForecastDto f) => new()
    {
        EventId = f.EventId,
        Feed = f.Feed,
        Title = f.Title,
        StartsUtc = AsUtc(f.StartsUtc),
        Outcome = f.Outcome,
        Probability = f.Probability,
        IsPremium = f.IsPremium,
    };

    // A time written with an offset is read as local time and turned back.
    // A time written with no offset at all is taken at its name: UTC.
    private static DateTime AsUtc(DateTime t) => t.Kind == DateTimeKind.Local
        ? t.ToUniversalTime()
        : DateTime.SpecifyKind(t, DateTimeKind.Utc);

    // SQLite gives a date back with no kind. It was stored as UTC, so say so,
    // or the JSON would carry it without its "Z".
    private static RecordedForecastDto ToDto(RecordRow r) => new(
        r.Title, DateTime.SpecifyKind(r.StartsUtc, DateTimeKind.Utc),
        r.Outcome, r.Probability, r.IsPremium, r.Happened);
}
