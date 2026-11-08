using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Foresight.Api.Accounts;
using Foresight.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// A login token whose account was deleted: signed, unexpired, and worth
/// nothing.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "7")]
public sealed class AccountCheckTests
{
    private readonly ApiFactory _api;

    public AccountCheckTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task WithoutTheCheck_TheTokenOfADeletedAccount_Works()
    {
        // The API with JwtBearer's own events: its own checks only.
        using var host = _api.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                o => o.EventsType = null)));
        var (client, email) = await OrphanedAsync(host);

        var me = await client.GetFromJsonAsync<Dictionary<string, string>>(
            "api/auth/me");

        Assert.Equal(email, me?["email"]);
    }

    [Fact]
    public async Task WithoutTheCheck_A401_HasNoSentence()
    {
        using var host = _api.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                o => o.EventsType = null)));
        using var client = host.CreateClient();

        var me = await client.GetAsync("api/auth/me");

        // JwtBearer's own answer: a status and a header, and no body.
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("Bearer", me.Headers.WwwAuthenticate.ToString());
        Assert.Empty(await me.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TheTokenOfADeletedAccount_Gets401_WithASentence()
    {
        var (client, _) = await OrphanedAsync(_api);

        var me = await client.GetAsync("api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal("Please sign in again.", await ApiError.ReadAsync(me));
    }

    // Signs an account in, then deletes the account behind the client's
    // back, with Identity itself, as any deletion would end. The client
    // still holds the token.
    private async Task<(HttpClient Client, string Email)> OrphanedAsync(
        WebApplicationFactory<Program> host)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        var token = await TokenForAsync(host, email);

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();
            var deleted = await users.DeleteAsync(
                (await users.FindByEmailAsync(email))!);
            Assert.True(deleted.Succeeded);
        }

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return (client, email);
    }

    private static async Task<string> TokenForAsync(
        WebApplicationFactory<Program> host, string email)
    {
        using var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<LoginResponse>())!
            .Token!;
    }
}
