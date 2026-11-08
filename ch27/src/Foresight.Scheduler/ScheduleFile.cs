using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foresight.Shared;

namespace Foresight.Scheduler;

/// <summary>What happened to one planned event.</summary>
public static class EventStatus
{
    public const string Pending = "pending";
    public const string Posted = "posted";
    public const string Failed = "failed";
    public const string NoForecast = "noforecast";
    public const string Expired = "expired";
}

/// <summary>One event of the day's plan, and what became of it.</summary>
public sealed class ScheduledEvent
{
    public string Feed { get; set; } = "";
    public string EventId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime StartsUtc { get; set; }
    public DateTime PostAtUtc { get; set; }

    // Only "pending" is ever posted. Every other status is final, and that
    // is the whole duplicate protection.
    public string Status { get; set; } = EventStatus.Pending;

    // The forecast exactly as it went out, recorded when it is posted.
    // The track record of chapter 19 reads this, never a forecast made
    // again later with whatever rules are live by then.
    public ForecastDto? Forecast { get; set; }
    public DateTime? PostedUtc { get; set; }

    // Why a post failed, in Telegram's words.
    public string? Note { get; set; }
}

/// <summary>The plan of one day in the readers' zone.</summary>
public sealed class DaySchedule
{
    public string Date { get; set; } = "";
    public List<ScheduledEvent> Events { get; set; } = [];
}

/// <summary>
/// Reads and writes schedule-YYYY-MM-DD.json in the state folder. Each run
/// of the scheduler is a new process, so this file is its memory. The
/// track record of chapter 19 reads the same files, with the names as
/// they are here: "Events", "Status", "Forecast".
/// </summary>
public sealed class ScheduleFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _folder;

    public ScheduleFile(string folder)
    {
        _folder = folder;
    }

    public string PathFor(DateOnly day) => Path.Combine(_folder,
        "schedule-" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        + ".json");

    public async Task<DaySchedule?> LoadAsync(
        DateOnly day, CancellationToken ct = default)
    {
        var path = PathFor(day);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DaySchedule>(
            stream, Json, ct);
    }

    public async Task SaveAsync(
        DaySchedule schedule, DateOnly day, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_folder);
        var path = PathFor(day);

        // Write a new file, then put it in place in one step. A crash in
        // the middle leaves the old file whole, never half of a new one.
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(
            temp, JsonSerializer.Serialize(schedule, Json), ct);
        File.Move(temp, path, overwrite: true);
    }
}
