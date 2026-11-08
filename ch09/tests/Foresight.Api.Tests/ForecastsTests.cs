using System.Net;
using System.Net.Http.Json;
using Foresight.Api.Feeds;
using Foresight.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The sample feed and its forecasts.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "1")]
public sealed class ForecastsTests
{
    private const string Weather = "api/forecasts/weather?day=2026-11-02";

    private readonly ApiFactory _api;

    public ForecastsTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task TheSampleFeed_GivesTheSameForecastsEveryTime()
    {
        using var client = _api.CreateClient();

        var first = await client.GetFromJsonAsync<List<ForecastDto>>(Weather);
        var second = await client.GetFromJsonAsync<List<ForecastDto>>(Weather);

        Assert.NotEmpty(first!);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task NobodyGetsPremiumForecastsYet()
    {
        using var client = _api.CreateClient();

        var forecasts =
            await client.GetFromJsonAsync<List<ForecastDto>>(Weather);

        Assert.All(forecasts!, f => Assert.False(f.IsPremium));
    }

    [Fact]
    public async Task AnUnknownFeed_Gets404()
    {
        using var client = _api.CreateClient();

        var response = await client.GetAsync("api/forecasts/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AUsedUpFeed_Gets503()
    {
        using var client = _api.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s =>
                s.AddSingleton<IEventSource, ExhaustedFeed>())).CreateClient();

        var response = await client.GetAsync(Weather);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class ExhaustedFeed : IEventSource
    {
        public IReadOnlyCollection<string> Feeds => ["weather"];

        public Task<IReadOnlyList<FeedEvent>> GetEventsAsync(
            string feed, DateOnly day, CancellationToken ct = default) =>
            throw new FeedLimitException("Used up.");
    }
}
