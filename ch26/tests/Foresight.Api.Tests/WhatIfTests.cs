using System.Net.Http.Json;
using Foresight.Api.Feeds;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The what-if endpoint, and what its answer gives away.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "24")]
public sealed class WhatIfTests
{
    // Two events of today's feed: premium at the feed's own number, and
    // free (TwoEvents below).
    private const string Strong = "weather-strong";
    private const string Calm = "weather-calm";

    private readonly ApiFactory _api;

    public WhatIfTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task ACountAtTheCallersNumber_GivesTheCutAway()
    {
        // The answer the endpoint must never give: whether there is a
        // premium forecast, worked out at the number the caller sent.
        var any = new FeedEvent("e", "weather", "", "", default, Score: 0);
        var search = await CutSearch.FindAsync(number => Task.FromResult(
            Forecaster.For(any with { Score = number }).IsPremium));

        Assert.Equal(80, search.Cut);
        Assert.Equal(7, search.Questions);
    }

    [Fact]
    public async Task TheHint_IsTheSameForEveryNumber()
    {
        using var api = WithTwoEvents();
        using var client = await SignedInAsync(api);

        var low = await AskAsync(client, Strong, 0);
        var below = await AskAsync(client, Strong, 79);
        var at = await AskAsync(client, Strong, 80);

        // The hint is the feed's; the free direction does not change at
        // the cut; and no probability goes out without premium.
        Assert.All([low, below, at], a => Assert.Equal(1, a.HiddenPremium));
        Assert.All([low, below, at], a => Assert.Null(a.Probability));
        Assert.Equal(below.Direction, at.Direction);
    }

    [Fact]
    public async Task TheSameSearch_AgainstTheEndpoint_FindsNoCut()
    {
        using var api = WithTwoEvents();
        using var client = await SignedInAsync(api);

        var search = await CutSearch.FindAsync(async number =>
            (await AskAsync(client, Calm, number)).HiddenPremium > 0);

        Assert.Null(search.Cut);
    }

    private WebApplicationFactory<Program> WithTwoEvents() =>
        _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IEventSource, TwoEvents>()));

    private static string NewEmail() =>
        $"user-{Guid.NewGuid():N}@example.test";

    // A signed-in client of the given API; the login token comes from the
    // shared API, which signs with the same key.
    private async Task<HttpClient> SignedInAsync(
        WebApplicationFactory<Program> api, string? email = null)
    {
        if (email is null)
        {
            email = NewEmail();
            await _api.RegisterConfirmedAsync(email);
        }

        using var login = await _api.SignedInClientAsync(email);
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            login.DefaultRequestHeaders.Authorization;
        return client;
    }

    private static async Task<WhatIfDto> AskAsync(
        HttpClient client, string eventId, int number)
    {
        using var response = await client.PostAsJsonAsync("api/what-if",
            new WhatIfRequest("weather", eventId, number));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WhatIfDto>())!;
    }

    private sealed class TwoEvents : IEventSource
    {
        public IReadOnlyCollection<string> Feeds => ["weather"];

        public Task<IReadOnlyList<FeedEvent>> GetEventsAsync(
            string feed, DateOnly day, CancellationToken ct = default)
        {
            var noon = day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
            return Task.FromResult<IReadOnlyList<FeedEvent>>(
            [
                new(Strong, feed, "Sun in Munich", "Sun", noon, Score: 90),
                new(Calm, feed, "Fog in London", "Fog", noon, Score: 30),
            ]);
        }
    }
}
