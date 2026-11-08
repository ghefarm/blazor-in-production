namespace Foresight.Scheduler.Tests;

/// <summary>Nothing goes out twice: the schedule file remembers what was
/// posted, and only one run works at a time.</summary>
[Trait("Chapter", "15")]
public sealed class PostOnceTests
{
    private static readonly DateTime Morning = new(2026, 11, 2, 7, 0, 0);
    private static readonly DateTime Eight = new(2026, 11, 2, 8, 0, 0);

    [Fact]
    public async Task AnEvent_IsPostedOnce()
    {
        using var bench = new Bench(Morning);
        bench.Api.Feeds["weather"] = [Bench.Forecast("w1", Eight)];
        await bench.Runner.PlanAsync();

        bench.Clock.Now = Eight.AddMinutes(-29);
        await bench.Runner.TickAsync(send: true, test: false);
        bench.Clock.Now = Eight.AddMinutes(-24);
        await bench.Runner.TickAsync(send: true, test: false);

        Assert.Single(bench.Telegram.Sent);
        var saved = await bench.Files.LoadAsync(bench.Runner.Today());
        Assert.Equal(EventStatus.Posted, saved!.Events[0].Status);
        Assert.Equal(0.6, saved.Events[0].Forecast!.Probability);
    }

    [Fact]
    public void ASecondRun_DoesNotGetTheLock_UntilTheFirstEnds()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "foresight-lock", Guid.NewGuid().ToString("N"));

        using (var first = RunLock.TryTake(folder))
        {
            Assert.NotNull(first);
            Assert.Null(RunLock.TryTake(folder));
        }

        using var later = RunLock.TryTake(folder);
        Assert.NotNull(later);
    }
}
