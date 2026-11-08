using Foresight.Telegram;

namespace Foresight.Scheduler.Tests;

/// <summary>A mailbox for the alarm: it keeps every mail, or fails.
/// </summary>
internal sealed class FakeMail : IAlarmMail
{
    public List<(string Subject, string Body)> Sent { get; } = new();
    public bool Down { get; set; }

    public Task SendAsync(
        string subject, string body, CancellationToken ct = default)
    {
        if (Down) throw new IOException("The mail server did not answer.");
        Sent.Add((subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>A refused post ends as "failed" and one mail, and a failure
/// that lasts sends no second one.</summary>
[Trait("Chapter", "16")]
public sealed class AlarmMailTests
{
    private static readonly DateTime Morning = new(2026, 11, 2, 7, 0, 0);
    private static readonly DateTime Eight = new(2026, 11, 2, 8, 0, 0);

    private static readonly TelegramException NotAMember = new(
        "Telegram refused sendMessage: Forbidden: bot is not a member of "
        + "the channel chat", answered: true);

    [Fact]
    public async Task ARefusedPost_IsFailed_MailedOnce_AndNeverTriedAgain()
    {
        using var bench = new Bench(Morning);
        bench.Api.Feeds["weather"] = [Bench.Forecast("w1", Eight)];
        await bench.Runner.PlanAsync();

        bench.Telegram.Fail = NotAMember;
        bench.Clock.Now = Eight.AddMinutes(-29);
        await bench.Runner.TickAsync(send: true, test: false);
        await bench.Alarm.FlushAsync();
        bench.Telegram.Fail = null;
        bench.NextRun();
        bench.Clock.Now = Eight.AddMinutes(-24);
        await bench.Runner.TickAsync(send: true, test: false);
        await bench.Alarm.FlushAsync();

        var saved = await bench.Files.LoadAsync(bench.Runner.Today());
        Assert.Equal(EventStatus.Failed, saved!.Events[0].Status);
        Assert.Null(saved.Events[0].Forecast); // not in the track record
        Assert.Empty(bench.Telegram.Sent);
        var mail = Assert.Single(bench.Mail.Sent);
        Assert.Contains("[free channel]: Event w1 was not posted", mail.Body);
        Assert.Contains("bot is not a member", mail.Body);
    }

    [Fact]
    public async Task AFailure_MailsOnce_WhileItLasts()
    {
        using var bench = new Bench(Morning);

        for (var run = 0; run < 3; run++)
        {
            bench.NextRun();
            bench.Alarm.Failed(Alarm.Feed("weather"), "Connection refused.");
            await bench.Alarm.FlushAsync();
        }

        var mail = Assert.Single(bench.Mail.Sent);
        Assert.Contains("1 problem", mail.Subject);
        Assert.Contains("[feed weather]: Connection refused.", mail.Body);
    }
}
