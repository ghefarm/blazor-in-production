using System.Globalization;
using System.Text.Json;

namespace Foresight.Scheduler;

/// <summary>The "Alarm" section: who is told when a run fails.</summary>
public sealed class AlarmSettings
{
    // Empty: no alarm mails at all.
    public string To { get; set; } = "";

    // A tick after this time of the readers' day that finds no schedule
    // file means the morning's plan failed. Before it, the plan may just
    // not have run yet.
    public string ScheduleDeadline { get; set; } = "09:30";
}

/// <summary>Sends one alarm mail. Tests use a fake.</summary>
public interface IAlarmMail
{
    Task SendAsync(string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Tells the owner by mail when the scheduler fails. Nobody reads the
/// output of a scheduled task, so without this the posts would stop and
/// nobody would know. The kinds of failure are in AlarmKinds.cs.
/// </summary>
public sealed partial class Alarm
{
    private readonly AlarmSettings _settings;
    private readonly IAlarmMail _mail;
    private readonly string _statePath;
    private readonly TimeProvider _clock;
    private readonly TextWriter _log;

    // What this run saw, by kind.
    private readonly Dictionary<string, string> _failed = new();
    private readonly HashSet<string> _worked = new();

    public Alarm(AlarmSettings settings, IAlarmMail mail, string folder,
        TimeProvider clock, TextWriter log)
    {
        _settings = settings;
        _mail = mail;
        _statePath = Path.Combine(folder, "alarm-state.json");
        _clock = clock;
        _log = log;
    }

    /// <summary>Something of this kind failed in this run. The first
    /// message of a run is the one that is kept.</summary>
    public void Failed(string kind, string message) =>
        _failed.TryAdd(kind, message);

    /// <summary>Something of this kind worked in this run.</summary>
    public void Worked(string kind) => _worked.Add(kind);

    /// <summary>
    /// At the end of a run: one mail for the kinds that start to fail and
    /// the kinds that work again, and nothing in between.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_settings.To.Length == 0) return;

        // Each run is a new process: what was already told lives in a file.
        var told = Load();
        var start = _failed.Where(f => !told.ContainsKey(f.Key)).ToList();
        // A kind that failed and worked in one run has not worked again.
        var back = _worked
            .Where(k => told.ContainsKey(k) && !_failed.ContainsKey(k))
            .ToList();
        if (start.Count + back.Count == 0) return;

        try
        {
            await _mail.SendAsync(Subject(start.Count), Body(start, back), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Nothing is marked as told, so the next run tries again.
            _log.WriteLine($"The alarm mail could not be sent: {ex.Message}");
            return;
        }

        foreach (var (kind, _) in start)
            told[kind] = _clock.GetUtcNow().UtcDateTime;
        foreach (var kind in back)
            told.Remove(kind);
        Save(told);
    }

    private static string Subject(int problems) => problems > 0
        ? $"Foresight scheduler: {problems} problem(s)"
        : "Foresight scheduler: working again";

    private string Body(
        List<KeyValuePair<string, string>> start, List<string> back)
    {
        var lines = new List<string>
        {
            "Time: " + _clock.GetUtcNow().UtcDateTime.ToString(
                "yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            "",
        };
        foreach (var (kind, message) in start)
            lines.Add($"Problem [{kind}]: {message}");
        foreach (var kind in back)
            lines.Add($"Working again [{kind}].");
        if (start.Count > 0)
        {
            lines.Add("");
            lines.Add("No further mail while this lasts. A mail comes when "
                + "it works again.");
        }
        return string.Join(Environment.NewLine, lines);
    }

    // Kind -> when its first mail went out. Only one run at a time writes
    // it: the run lock of chapter 15 keeps the others out.
    private Dictionary<string, DateTime> Load()
    {
        try
        {
            return File.Exists(_statePath)
                ? JsonSerializer.Deserialize<Dictionary<string, DateTime>>(
                    File.ReadAllText(_statePath)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new(); // a damaged file costs one mail too many, no more
        }
    }

    private void Save(Dictionary<string, DateTime> told)
    {
        // The same way as the schedule file: a new file, then one move.
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var temp = _statePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(told));
        File.Move(temp, _statePath, overwrite: true);
    }
}
