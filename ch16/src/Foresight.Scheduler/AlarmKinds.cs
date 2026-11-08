using System.Globalization;
using Foresight.Telegram;

namespace Foresight.Scheduler;

/// <summary>
/// The kinds of failure the scheduler reports, and their words. Each kind
/// is as narrow as the thing that must work again before "working again"
/// is true: a post to the free channel says nothing about the premium
/// one, and one feed that answers says nothing about another.
/// </summary>
public sealed partial class Alarm
{
    public const string Run = "run";
    public const string Schedule = "schedule";

    public static string Feed(string feed) => $"feed {feed}";

    public static string Channel(string tier) => $"{tier} channel";

    /// <summary>A post did not go out. Whether Telegram answered decides
    /// what the owner may do about it (chapter 13).</summary>
    public void NotPosted(string title, string tier, TelegramException ex)
    {
        var words = ex.Message.TrimEnd('.');
        Failed(Channel(tier), ex.Answered
            ? $"{title} was not posted: {words}. Nothing reached the "
                + "channel; post it by hand once the cause is fixed."
            : $"{title}: {words}. The post may have arrived anyway; look "
                + "at the channel before you post it by hand.");
    }

    /// <summary>A tick found no schedule for today. Before the deadline
    /// the plan may not have run yet; after it, the plan failed.</summary>
    public void NoSchedule(DateOnly day, TimeZoneInfo readersZone)
    {
        var readersNow = TimeZoneInfo.ConvertTime(
            _clock.GetUtcNow(), readersZone);
        var deadline = TimeOnly.Parse(
            _settings.ScheduleDeadline, CultureInfo.InvariantCulture);
        if (TimeOnly.FromDateTime(readersNow.DateTime) < deadline) return;

        Failed(Schedule, "There is no schedule for " + day.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture) + ". The plan did "
            + "not write one, so nothing is posted today.");
    }
}
