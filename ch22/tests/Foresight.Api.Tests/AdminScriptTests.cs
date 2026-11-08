using System.Net;
using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>
/// The admin tools behind the API's gate, called the way a script calls
/// them: without the page.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "14")]
public sealed class AdminScriptTests
{
    private readonly ApiFactory _api;

    public AdminScriptTests(ApiFactory api)
    {
        _api = api;
    }

    private static string NewEmail() =>
        $"user-{Guid.NewGuid():N}@example.test";

    // A sign-in with the password alone.
    private async Task<LoginResponse> LoginAsync(string email)
    {
        using var client = _api.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    [Fact]
    public async Task AnAccountsOwnToken_OpensNoAdminTool()
    {
        var victim = NewEmail();
        var caller = NewEmail();
        await _api.RegisterConfirmedAsync(victim);
        await _api.RegisterConfirmedAsync(caller);
        using var script = await _api.SignedInClientAsync(caller);

        var calls = new[]
        {
            script.GetAsync("api/admin/accounts"),
            script.PostAsJsonAsync("api/admin/accounts/delete",
                new AdminAccountRequest(victim)),
            script.PostAsJsonAsync("api/admin/accounts/two-factor-off",
                new AdminAccountRequest(victim)),
            script.PostAsJsonAsync("api/scheduler/posting",
                new PostingSwitch(false)),
            script.PostAsJsonAsync("api/channels/post",
                new ChannelPost(true, "free premium for all")),
        };

        foreach (var answer in await Task.WhenAll(calls))
            Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        Assert.NotNull((await LoginAsync(victim)).Token);
    }
}
