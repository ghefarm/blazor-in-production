using Foresight.Api.TrackRecord;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The track record: what was posted, against what happened.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "19")]
public sealed class TrackRecordRulesTests
{
    private static readonly DateTime Yesterday =
        DateTime.UtcNow.Date.AddDays(-1);

    private readonly ApiFactory _api;
    private readonly Posted _posted = new();
    private readonly Outcomes _outcomes = new();

    public TrackRecordRulesTests(ApiFactory api)
    {
        _api = api;
    }

    // The API with posted forecasts and outcomes that the test chooses.
    private WebApplicationFactory<Program> Host() =>
        _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IPostedForecasts>(_posted);
            s.AddSingleton<IOutcomeSource>(_outcomes);
        }));

    private static ForecastDto Forecast(string id, string title,
        DateTime starts, double probability, bool premium = false) =>
        new(id, "weather", title, starts, "Rain", probability, premium);

    [Fact]
    public async Task TheRecord_ReadsWhatTheSchedulerPosted()
    {
        var folder = Path.Combine(Path.GetTempPath(), "foresight-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(
            Path.Combine(folder, "schedule-2026-11-02.json"), """
            { "Events": [
              { "Status": "posted", "Forecast": { "EventId": "w-1",
                "Feed": "weather", "Title": "Rain in Hamburg",
                "StartsUtc": "2026-11-02T08:00:00Z", "Outcome": "Rain",
                "Probability": 0.66, "IsPremium": false } },
              { "Status": "pending", "Forecast": null },
              { "Status": "expired" } ] }
            """);
        using var host = _api.WithWebHostBuilder(b => b.ConfigureTestServices(
            s => s.Configure<TrackRecordOptions>(
                o => o.ScheduleFolder = folder)));

        var posted = await host.Services
            .GetRequiredService<IPostedForecasts>().ReadAsync();

        var forecast = Assert.Single(posted);
        Assert.Equal(0.66, forecast.Probability);
        Assert.Equal(DateTimeKind.Utc, forecast.StartsUtc.Kind);
    }

    [Fact]
    public async Task TwoOutcomesThatFit_LeaveTheForecastOpen_AndNamed()
    {
        var at = Yesterday.AddHours(10);
        _posted.Items.Add(Forecast("w-1", "Rain in Hamburg", at, 0.8));
        _outcomes.Items.AddRange([
            new("weather", "HAMBURG", at.AddHours(1), true),
            new("weather", "HAMBURG", at.AddHours(2), false),
            new("weather", "VIENNA", at.AddHours(8), false)]);
        using var host = Host();
        var record = host.Services.GetRequiredService<TrackRecordService>();

        var report = await record.RefreshAsync();

        Assert.Equal(1, report.Open);
        Assert.Contains("Rain in Hamburg", Assert.Single(report.Missing));
    }

    [Fact]
    public async Task APremiumForecast_WaitsForItsResult()
    {
        var at = DateTime.UtcNow.AddHours(3);
        _posted.Items.AddRange([
            Forecast("w-1", "Rain in Hamburg", at, 0.9, premium: true),
            Forecast("w-2", "Rain in Munich", at, 0.6)]);
        using var host = Host();
        var record = host.Services.GetRequiredService<TrackRecordService>();

        await record.RefreshAsync();
        var before = await record.GetAsync("weather");
        _outcomes.Items.Add(new("weather", "HAMBURG", at.AddHours(1), true));
        await record.RefreshAsync();
        var after = await record.GetAsync("weather");

        Assert.Equal(["Rain in Munich"], before.Forecasts.Select(f => f.Title));
        Assert.Equal(2, after.Forecasts.Count);
    }

    private sealed class Posted : IPostedForecasts
    {
        public List<ForecastDto> Items { get; } = [];

        public Task<IReadOnlyList<ForecastDto>> ReadAsync(
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ForecastDto>>(Items.ToList());
    }

    private sealed class Outcomes : IOutcomeSource
    {
        public List<RecordedOutcome> Items { get; } = [];

        public Task<IReadOnlyList<RecordedOutcome>> GetAsync(string feed,
            DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RecordedOutcome>>(
                Items.Where(o => o.Feed == feed).ToList());
    }
}
