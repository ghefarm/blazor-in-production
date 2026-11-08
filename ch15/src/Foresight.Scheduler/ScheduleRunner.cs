using System.Globalization;
using Foresight.Shared;
using Foresight.Telegram;

namespace Foresight.Scheduler;

/// <summary>
/// The two jobs of the scheduler: plan the day, and post what has come
/// due. Every run is a new process; the schedule file is its memory.
/// </summary>
public sealed class ScheduleRunner
{
    private readonly SchedulerSettings _settings;
    private readonly ISchedulerApi _api;
    private readonly ITelegramSender _telegram;
    private readonly ScheduleFile _files;
    private readonly TimeProvider _clock;
    private readonly TextWriter _log;
    private readonly TimeSpan _pause;
    private readonly TimeZoneInfo _zone;

    /// <param name="pause">The wait after each post: a little over a
    /// second, for Telegram's limit per chat (chapter 13).</param>
    public ScheduleRunner(
        SchedulerSettings settings,
        ISchedulerApi api,
        ITelegramSender telegram,
        ScheduleFile files,
        TimeProvider clock,
        TextWriter log,
        TimeSpan pause)
    {
        _settings = settings;
        _api = api;
        _telegram = telegram;
        _files = files;
        _clock = clock;
        _log = log;
        _pause = pause;
        _zone = settings.Telegram.Zone;
    }

    /// <summary>Today in the readers' zone. The host's own date may be
    /// another day.</summary>
    public DateOnly Today() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _zone).DateTime);

    /// <summary>
    /// The kill switch of chapter 14. It fails open: only a deliberate
    /// "off" stops the posts, never a switch that could not be read.
    /// </summary>
    public async Task<bool> PostingIsOnAsync(CancellationToken ct = default)
    {
        try
        {
            return await _api.IsPostingOnAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log($"The switch could not be read ({ex.Message}). Posting.");
            return true;
        }
    }

    public async Task PlanAsync(CancellationToken ct = default)
    {
        var day = Today();
        var now = _clock.GetUtcNow().UtcDateTime;

        // A second plan on the same day keeps what the first one wrote:
        // an event that went out must stay "posted".
        var schedule = await _files.LoadAsync(day, ct)
            ?? new DaySchedule { Date = Iso(day) };

        foreach (var feed in _settings.Feeds)
        {
            var forecasts = await FetchAsync(feed, day, ct);
            if (forecasts is null) continue;

            // Only the event and its start are planned. A real feed's
            // numbers move until shortly before the start, so the forecast
            // itself is fetched again when the event is due.
            foreach (var f in forecasts.Where(f => f.StartsUtc > now))
            {
                if (schedule.Events.Any(e => e.EventId == f.EventId))
                    continue;
                schedule.Events.Add(new ScheduledEvent
                {
                    Feed = feed, EventId = f.EventId, Title = f.Title,
                    StartsUtc = f.StartsUtc,
                    PostAtUtc = f.StartsUtc.AddMinutes(-_settings.LeadMinutes),
                });
            }
        }

        schedule.Events.Sort((a, b) => a.StartsUtc.CompareTo(b.StartsUtc));
        await _files.SaveAsync(schedule, day, ct);

        Log($"Planned {schedule.Events.Count} event(s) for {Iso(day)}.");
        foreach (var e in schedule.Events)
            Log($"  post {Local(e.PostAtUtc)}, starts {Local(e.StartsUtc)}: "
                + $"{e.Title} ({e.Status})");
    }

    public async Task TickAsync(
        bool send, bool test, CancellationToken ct = default)
    {
        var day = Today();
        var schedule = await _files.LoadAsync(day, ct);
        if (schedule is null)
        {
            Log($"No schedule for {Iso(day)}. Run --plan first.");
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var due = schedule.Events
            .Where(e => e.Status == EventStatus.Pending && now >= e.PostAtUtc)
            .ToList();
        if (due.Count == 0)
        {
            Log("Nothing due.");
            return;
        }

        // One feed call per feed in this tick, however many of its events
        // are due: the feed has a monthly limit.
        foreach (var feed in due.GroupBy(e => e.Feed))
        {
            // Late, but not too late: after the grace, never post.
            var grace = TimeSpan.FromMinutes(_settings.LateGraceMinutes);
            var late = feed.Where(e => now > e.StartsUtc + grace).ToList();
            foreach (var e in late)
                await MarkAsync(schedule, day, e, EventStatus.Expired,
                    send, ct);

            var ready = feed.Except(late).ToList();
            if (ready.Count == 0) continue;

            var forecasts = await FetchAsync(feed.Key, day, ct);
            if (forecasts is null) continue; // still pending: next tick

            foreach (var e in ready)
                await PostAsync(schedule, day, e,
                    forecasts.FirstOrDefault(f => f.EventId == e.EventId),
                    send, test, ct);
        }
    }

    private async Task PostAsync(DaySchedule schedule, DateOnly day,
        ScheduledEvent e, ForecastDto? f, bool send, bool test,
        CancellationToken ct)
    {
        if (f is null)
        {
            await MarkAsync(schedule, day, e, EventStatus.NoForecast,
                send, ct);
            return;
        }

        var tier = f.IsPremium ? "premium" : "free";
        var text = ForecastMessage.For(
            f, _zone, test ? ForecastMessage.TestBanner : null);
        if (!send)
        {
            Log($"[dry run] would post {e.Title} to the {tier} channel:");
            Log(text);
            return;
        }

        try
        {
            await _telegram.SendAsync(
                _settings.Telegram.ChannelFor(f.IsPremium), text, ct);
            e.Status = EventStatus.Posted;
            e.Forecast = f;
            e.PostedUtc = _clock.GetUtcNow().UtcDateTime;
            Log($"Posted {e.Title} to the {tier} channel.");
        }
        catch (TelegramException ex)
        {
            // One try per event. Without an answer the post may have
            // arrived, and a second try could post it twice.
            e.Status = EventStatus.Failed;
            e.Note = ex.Message;
            Log($"FAILED {e.Title}: {ex.Message}");
        }

        // Saved after every post, not once at the end: a crash between two
        // posts must not let the next tick post the first one again.
        await _files.SaveAsync(schedule, day, ct);
        await Task.Delay(_pause, ct);
    }

    private async Task MarkAsync(DaySchedule schedule, DateOnly day,
        ScheduledEvent e, string status, bool send, CancellationToken ct)
    {
        Log($"{e.Title}: {status}{(send ? "" : " (dry run, not saved)")}.");
        if (!send) return;
        e.Status = status;
        await _files.SaveAsync(schedule, day, ct);
    }

    // The forecasts of one feed, or null when the API did not answer.
    private async Task<IReadOnlyList<ForecastDto>?> FetchAsync(
        string feed, DateOnly day, CancellationToken ct)
    {
        try
        {
            return await _api.ForecastsAsync(feed, day, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log($"{feed}: no forecasts from the API: {ex.Message}");
            return null;
        }
    }

    private string Local(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, _zone)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly day) =>
        day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private void Log(string line) =>
        _log.WriteLine($"[{Local(_clock.GetUtcNow().UtcDateTime)}] {line}");
}
