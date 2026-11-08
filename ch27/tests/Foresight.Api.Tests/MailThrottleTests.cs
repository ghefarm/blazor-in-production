using System.Net.Http.Json;
using Foresight.Api.Accounts;
using Foresight.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The throttle holds for every door to a confirmation mail.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "6")]
public sealed class MailThrottleTests
{
    private readonly ApiFactory _api;

    public MailThrottleTests(ApiFactory api)
    {
        _api = api;
    }

    private int MailsTo(string email) =>
        Directory.GetFiles(_api.MailFolder, "*.eml")
            .Count(f => File.ReadAllText(f).StartsWith($"To: {email}"));

    [Fact]
    public async Task RegisterDeleteRegister_StartsNoNewRound()
    {
        using var client = _api.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.test";

        // Whoever registers an address chooses its password, so they can
        // also delete the account and register it again, as often as
        // they like.
        for (var i = 0; i < 3; i++)
        {
            var registered = await client.PostAsJsonAsync(
                "api/auth/register",
                new RegisterRequest(email, ApiFactory.Password));
            registered.EnsureSuccessStatusCode();
            await DeleteAccountAsync(email);
        }

        Assert.Equal(1, MailsTo(email));
    }

    // Deletes the account with Identity itself, the step every deletion
    // ends with (chapter 11 builds the endpoint around it).
    private async Task DeleteAccountAsync(string email)
    {
        using var scope = _api.Services.CreateScope();
        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();
        var deleted = await users.DeleteAsync(
            (await users.FindByEmailAsync(email))!);
        Assert.True(deleted.Succeeded);
    }
}
