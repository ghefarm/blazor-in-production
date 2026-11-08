using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Foresight.Api.Poll;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The poll: one row per account, and a token read everywhere.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "18")]
public sealed class VoteTests
{
    private readonly ApiFactory _api;

    public VoteTests(ApiFactory api)
    {
        _api = api;
    }

    private static string NewEmail() =>
        $"voter-{Guid.NewGuid():N}@example.test";

    // The API with a round of its own, so the counts of one test are not
    // the counts of another. The settings' candidates stay.
    private WebApplicationFactory<Program> Round() =>
        _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.Configure<PollOptions>(
                o => o.Round = Guid.NewGuid().ToString("N"))));

    [Fact]
    public async Task TenVotesAtTheSameMoment_LeaveOneRow()
    {
        using var host = Round();
        var store = host.Services.GetRequiredService<PollStore>();
        var email = NewEmail();

        // A double click, ten times over: ten threads that start together,
        // so several find no row and try to insert one. (Without Task.Run
        // they would not overlap: SQLite's async calls finish at once.)
        using var start = new Barrier(10);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(
            () =>
            {
                start.SignalAndWait();
                return store.VoteAsync(email, i % 2 == 0 ? "air" : "tides");
            })));

        var poll = await store.GetAsync(email);
        Assert.Equal(1, poll.TotalVotes);
    }

    // Signed in on the test's own host, so its votes go into its round.
    private async Task<HttpClient> SignedInAsync(
        WebApplicationFactory<Program> host)
    {
        var email = NewEmail();
        await _api.RegisterConfirmedAsync(email);
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));
        var token = (await login.Content
            .ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> Vote(
        HttpClient client, string candidate) =>
        client.PutAsJsonAsync("api/poll/vote", new VoteRequest(candidate));

    // Framework facts: the default scheme reads the token on every
    // request, also where no login is required.
    [Fact]
    public async Task AnAnonymousEndpoint_StillReadsAValidToken()
    {
        using var host = Round();
        using var client = await SignedInAsync(host);
        await Vote(client, "air");

        var poll = await client.GetFromJsonAsync<PollDto>("api/poll");

        Assert.Equal("air", poll!.MyVote);
    }

    [Fact]
    public async Task ABadToken_OnAnAnonymousEndpoint_IsJustAnonymous()
    {
        using var host = Round();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.token");

        var read = await client.GetAsync("api/poll");
        var vote = await Vote(client, "air");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, vote.StatusCode);
    }
}
