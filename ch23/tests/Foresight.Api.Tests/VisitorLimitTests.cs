using System.Net;
using System.Net.Http.Json;
using Foresight.Api.Defenses;
using Foresight.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Tests;

/// <summary>
/// The limits as Foresight ships them, with its two trusted callers.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "22")]
public sealed class VisitorLimitTests
{
    // As long as the API asks for (TrustedCallers.MinKeyLength).
    public const string WebsiteKey = "a-website-key-of-at-least-32-characters";
    public const string SchedulerKey = PostingSwitchTests.Key;

    // A day of the sample feed, and one of its events.
    public static readonly DateTimeOffset Day =
        new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    public const string EventId = "weather-20261106-1";

    private readonly ApiFactory _api;

    public VisitorLimitTests(ApiFactory api)
    {
        _api = api;
    }

    /// <summary>
    /// The API with the shipped limits, a fixed day, and the two keys in
    /// its settings, as on the host.
    /// </summary>
    public static WebApplicationFactory<Program> Shipped(ApiFactory api,
        Action<IServiceCollection>? more = null) =>
        api.WithWebHostBuilder(b => b
            .UseSetting(Key("WebsiteKey"), WebsiteKey)
            .UseSetting(Key("SchedulerKey"), SchedulerKey)
            .ConfigureTestServices(s =>
            {
                s.AddSingleton(Options.Create(new RateLimitOptions()));
                s.AddSingleton<TimeProvider>(new FixedClock(Day));
                more?.Invoke(s);
            }));

    private static string Key(string name) =>
        $"{TrustedCallerOptions.SectionName}:{name}";

    [Fact]
    public async Task EachVisitorOfTheWebsite_CountsAlone()
    {
        using var api = Shipped(_api);
        using var website = api.CreateClient();
        website.DefaultRequestHeaders.Add(ApiHeaders.WebsiteKey, WebsiteKey);

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK,
                (await AskAsync(website, visitor: "192.0.2.10")).StatusCode);
        using var refused = await AskAsync(website, visitor: "192.0.2.10");
        using var other = await AskAsync(website, visitor: "192.0.2.11");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.StartsWith("Too many requests",
            (await refused.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task OnlyTheScheduler_MaySpendTheFeedsReserve()
    {
        // A reserve larger than the month: only the reserve is left.
        using var api = Shipped(_api, s => s.AddSingleton(
            Options.Create(new RateLimitOptions { FeedReserve = 1_000_000 })));
        using var website = api.CreateClient();
        website.DefaultRequestHeaders.Add(ApiHeaders.WebsiteKey, WebsiteKey);
        using var scheduler = api.CreateClient();
        scheduler.DefaultRequestHeaders.Add(
            ApiHeaders.SchedulerKey, SchedulerKey);

        using var refused = await website.GetAsync("api/forecasts/weather");
        using var served = await scheduler.GetAsync("api/forecasts/weather");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    /// <summary>A what-if question about a real event, as one visitor.
    /// </summary>
    public static Task<HttpResponseMessage> AskAsync(
        HttpClient client, string? visitor, int score = 50)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/what-if")
        {
            Content = JsonContent.Create(
                new WhatIfRequest("weather", EventId, score)),
        };
        if (visitor is not null)
            request.Headers.Add(ApiHeaders.VisitorIp, visitor);
        return client.SendAsync(request);
    }
}
