using Foresight.Telegram;

namespace Foresight.Scheduler;

/// <summary>
/// The scheduler's settings: appsettings.json, then the host's own
/// appsettings.Production.json, then environment variables that start
/// with FORESIGHT_. The key and the bot token come only from the host.
/// </summary>
public sealed class SchedulerSettings
{
    public string ApiBaseUrl { get; set; } = "";

    // Sent as X-Scheduler-Key. The scheduler's only credential: there is
    // no account and no password.
    public string ApiKey { get; set; } = "";

    public List<string> Feeds { get; set; } = [];

    // An event is posted this many minutes before it starts.
    public int LeadMinutes { get; set; } = 30;

    // How long after its start an event that was never posted may still
    // go out. After that it is marked expired.
    public int LateGraceMinutes { get; set; } = 10;

    // Where the schedule files live. Empty: next to the program. On the
    // host, the folder the API's track record reads (chapter 19).
    public string StateFolder { get; set; } = "";

    public TelegramOptions Telegram { get; set; } = new();

    // Who is told when a run fails, and the mail server that tells them
    // (chapter 16). The mail password comes only from the host.
    public AlarmSettings Alarm { get; set; } = new();
    public SmtpSettings Smtp { get; set; } = new();

    public string Folder => StateFolder.Length > 0
        ? StateFolder
        : AppContext.BaseDirectory;
}
