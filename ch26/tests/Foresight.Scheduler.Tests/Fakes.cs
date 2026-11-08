using Foresight.Shared;
using Foresight.Telegram;

namespace Foresight.Scheduler.Tests;

/// <summary>A clock that stands still until a test moves it.</summary>
internal sealed class FixedClock : TimeProvider
{
    public FixedClock(DateTime utc)
    {
        Now = utc;
    }

    public DateTime Now { get; set; }

    public override DateTimeOffset GetUtcNow() =>
        new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
}

/// <summary>The API for the scheduler's tests: lists set by the test.
/// </summary>
internal sealed class FakeApi : ISchedulerApi
{
    public Dictionary<string, List<ForecastDto>> Feeds { get; } = new();
    public int ForecastCalls { get; private set; }
    public bool? PostingOn { get; set; } = true;
    public bool Down { get; set; }

    public Task<IReadOnlyList<ForecastDto>> ForecastsAsync(
        string feed, DateOnly day, CancellationToken ct = default)
    {
        if (Down) throw new HttpRequestException("Connection refused.");
        ForecastCalls++;
        IReadOnlyList<ForecastDto> list = Feeds.GetValueOrDefault(feed) ?? [];
        return Task.FromResult(list);
    }

    public Task<bool> IsPostingOnAsync(CancellationToken ct = default) =>
        PostingOn is { } on
            ? Task.FromResult(on)
            : throw new HttpRequestException("The request timed out.");
}

/// <summary>Telegram for the scheduler's tests.</summary>
internal sealed class FakeTelegram : ITelegramSender
{
    public List<(string Chat, string Text)> Sent { get; } = new();

    /// <summary>Set to make every send fail like this.</summary>
    public TelegramException? Fail { get; set; }

    public Task SendAsync(
        string chat, string text, CancellationToken ct = default)
    {
        if (Fail is { } failure) throw failure;
        Sent.Add((chat, text));
        return Task.CompletedTask;
    }
}

/// <summary>The scheduler in a temporary folder, with the fakes.</summary>
internal sealed class Bench : IDisposable
{
    public const string Free = "@foresight_free";
    public const string Premium = "-1001234567890";

    public Bench(DateTime nowUtc)
    {
        Clock = new FixedClock(nowUtc);
        Settings = new SchedulerSettings
        {
            ApiBaseUrl = "http://127.0.0.1:9/",
            ApiKey = "unused",
            Feeds = ["weather", "transit"],
            StateFolder = Path.Combine(Path.GetTempPath(),
                "foresight-scheduler", Guid.NewGuid().ToString("N")),
            Telegram = new TelegramOptions
            {
                BotToken = "123456:TEST-TOKEN",
                FreeChannel = Free,
                PremiumChannel = Premium,
                TimeZone = "Europe/Berlin",
            },
        };
        Settings.Alarm.To = "owner@example.test";
        Files = new ScheduleFile(Settings.Folder);
        NextRun();
    }

    /// <summary>A new process, as on the host: a new alarm and runner on
    /// the same folder (chapter 16).</summary>
    public void NextRun()
    {
        Alarm = new Alarm(Settings.Alarm, Mail, Settings.Folder, Clock, Log);
        Runner = new ScheduleRunner(Settings, Api, Telegram, Files, Alarm,
            Clock, Log, TimeSpan.Zero);
    }

    public FixedClock Clock { get; }
    public SchedulerSettings Settings { get; }
    public FakeApi Api { get; } = new();
    public FakeTelegram Telegram { get; } = new();
    public ScheduleFile Files { get; }
    public StringWriter Log { get; } = new();
    public FakeMail Mail { get; } = new();
    public Alarm Alarm { get; private set; } = null!;
    public ScheduleRunner Runner { get; private set; } = null!;

    public static ForecastDto Forecast(string id, DateTime startsUtc,
        double probability = 0.6, string feed = "weather") =>
        new(id, feed, "Event " + id, startsUtc, "Rain", probability,
            IsPremium: probability >= 0.75);

    public void Dispose()
    {
        if (Directory.Exists(Settings.Folder))
            Directory.Delete(Settings.Folder, recursive: true);
    }
}
