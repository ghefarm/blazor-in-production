using System.Net;
using System.Net.Http.Json;
using Foresight.Api.Defenses;
using Foresight.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Foresight.Api.Tests;

/// <summary>The kill switch: set by the admin, read by the scheduler.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "14")]
public sealed class PostingSwitchTests
{
    public const string Key = "a-scheduler-key-of-at-least-32-characters";

    private readonly ApiFactory _api;

    public PostingSwitchTests(ApiFactory api)
    {
        _api = api;
    }

    /// <summary>The API with a scheduler key in its settings, as on the
    /// host.</summary>
    public static WebApplicationFactory<Program> WithKey(ApiFactory api) =>
        api.WithWebHostBuilder(b => b.UseSetting(
            $"{TrustedCallerOptions.SectionName}:SchedulerKey", Key));

    private WebApplicationFactory<Program> Host() => WithKey(_api);

    private static async Task<bool?> ReadAsync(HttpClient client)
    {
        var answer = await client.GetAsync("api/scheduler/posting");
        return answer.IsSuccessStatusCode
            ? (await answer.Content.ReadFromJsonAsync<PostingSwitch>())!.On
            : null;
    }

    [Fact]
    public async Task TheScheduler_ReadsWhatTheAdminSet()
    {
        var host = Host();
        await _api.EnsureAdminAsync();
        using var admin = await ChannelHost.SignedInAsync(
            host, ApiFactory.AdminEmail);
        using var scheduler = host.CreateClient();
        scheduler.DefaultRequestHeaders.Add(ApiHeaders.SchedulerKey, Key);

        try
        {
            (await admin.PostAsJsonAsync("api/scheduler/posting",
                new PostingSwitch(false))).EnsureSuccessStatusCode();
            Assert.False(await ReadAsync(scheduler));
        }
        finally
        {
            // The switch lives in the shared test database: leave it on.
            (await admin.PostAsJsonAsync("api/scheduler/posting",
                new PostingSwitch(true))).EnsureSuccessStatusCode();
        }
        Assert.True(await ReadAsync(scheduler));
    }

    [Fact]
    public async Task AWrongKey_LearnsNothing()
    {
        using var stranger = Host().CreateClient();
        stranger.DefaultRequestHeaders.Add(
            ApiHeaders.SchedulerKey, Key.Replace('a', 'b'));

        var answer = await stranger.GetAsync("api/scheduler/posting");

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
    }
}
