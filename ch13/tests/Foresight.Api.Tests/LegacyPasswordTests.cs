using System.Net.Http.Json;
using System.Security.Cryptography;
using Foresight.Api.Accounts;
using Foresight.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>Accounts from the first, hand-built login keep working.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "4")]
public sealed class LegacyPasswordTests
{
    private readonly ApiFactory _api;

    public LegacyPasswordTests(ApiFactory api)
    {
        _api = api;
    }

    // A hash in the first login's format: pbkdf2$iterations$salt$hash.
    private static string OldHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000,
            HashAlgorithmName.SHA256, 32);
        return $"pbkdf2$100000${Convert.ToBase64String(salt)}"
            + $"${Convert.ToBase64String(hash)}";
    }

    private async Task<string?> StoredHashAsync(string email)
    {
        using var scope = _api.Services.CreateScope();
        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();
        return (await users.FindByEmailAsync(email))!.PasswordHash;
    }

    [Fact]
    public async Task AnOldHash_SignsIn_AndIsReplaced()
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        using (var scope = _api.Services.CreateScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            user.PasswordHash = OldHash(ApiFactory.Password);
            await users.UpdateAsync(user);
        }

        using var client = _api.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));

        Assert.NotNull((await login.Content
            .ReadFromJsonAsync<LoginResponse>())!.Token);
        Assert.DoesNotContain("pbkdf2$", await StoredHashAsync(email));
    }
}
