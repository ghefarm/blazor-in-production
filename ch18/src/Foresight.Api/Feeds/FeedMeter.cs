using System.Globalization;
using Foresight.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Feeds;

/// <summary>
/// Counts the feed's requests per calendar month and stops at the limit.
/// The count is kept in the database: a limit that a restart resets is no
/// limit.
/// </summary>
public sealed class FeedMeter
{
    // One count at a time, so two requests cannot both take the last one.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly FeedOptions _options;
    private readonly TimeProvider _clock;

    public FeedMeter(
        IDbContextFactory<AppDbContext> db,
        IOptions<FeedOptions> options,
        TimeProvider clock)
    {
        _db = db;
        _options = options.Value;
        _clock = clock;
    }

    /// <summary>How many requests are left this month.</summary>
    public async Task<int> RemainingAsync(CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        var usage = await db.FeedUsage.FindAsync([ThisMonth()], ct);
        return _options.MonthlyLimit - (usage?.Requests ?? 0);
    }

    /// <summary>Counts one request, or throws when none are left.</summary>
    public async Task CountAsync(CancellationToken ct = default)
    {
        var month = ThisMonth();

        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await _db.CreateDbContextAsync(ct);
            var usage = await db.FeedUsage.FindAsync([month], ct);
            if (usage is null)
            {
                usage = new FeedUsage { Month = month };
                db.FeedUsage.Add(usage);
            }

            if (usage.Requests >= _options.MonthlyLimit)
                throw new FeedLimitException(
                    $"The feed's {_options.MonthlyLimit} requests for {month}"
                    + " are used up.");

            usage.Requests++;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ThisMonth() => _clock.GetUtcNow()
        .ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
