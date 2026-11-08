using Foresight.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Foresight.Api.Scheduling;

/// <summary>An on/off setting the admin changes at run time.</summary>
public sealed class SwitchRow
{
    public string Name { get; set; } = "";
    public bool IsOn { get; set; }
    public DateTime ChangedUtc { get; set; }

    // Who changed it last, for the day someone asks why posting stopped.
    public string ChangedBy { get; set; } = "";
}

/// <summary>The switches, in the one database.</summary>
public sealed class SwitchStore
{
    /// <summary>Whether the scheduler may post.</summary>
    public const string Posting = "posting";

    private readonly IDbContextFactory<AppDbContext> _db;
    private readonly TimeProvider _clock;

    public SwitchStore(IDbContextFactory<AppDbContext> db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>A switch nobody has touched yet is on.</summary>
    public async Task<bool> IsOnAsync(
        string name, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        var row = await db.Switches.FindAsync([name], ct);
        return row?.IsOn ?? true;
    }

    public async Task SetAsync(
        string name, bool on, string by, CancellationToken ct = default)
    {
        await using var db = await _db.CreateDbContextAsync(ct);
        var row = await db.Switches.FindAsync([name], ct);
        if (row is null)
        {
            row = new SwitchRow { Name = name };
            db.Switches.Add(row);
        }

        row.IsOn = on;
        row.ChangedUtc = _clock.GetUtcNow().UtcDateTime;
        row.ChangedBy = by;
        await db.SaveChangesAsync(ct);
    }
}
