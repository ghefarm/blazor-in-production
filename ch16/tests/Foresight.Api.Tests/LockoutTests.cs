using System.Net;
using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>The lock: wrong passwords and wrong codes count alike.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "4")]
public sealed class LockoutTests
{
    private readonly ApiFactory _api;

    public LockoutTests(ApiFactory api)
    {
        _api = api;
    }

    private static string NewEmail() =>
        $"user-{Guid.NewGuid():N}@example.test";

    // A code that is surely wrong: the right one with its last digit moved.
    private static string WrongCode(string key)
    {
        var right = Totp.Code(key);
        return right[..5] + (char)('0' + (right[5] - '0' + 1) % 10);
    }

    private async Task<HttpResponseMessage> SignIn(string email,
        string password = ApiFactory.Password, string? code = null)
    {
        using var client = _api.CreateClient();
        return await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, password, code));
    }

    [Fact]
    public async Task ThreeWrongPasswords_LockTheAccount()
    {
        var email = NewEmail();
        await _api.RegisterConfirmedAsync(email);

        for (var i = 0; i < 3; i++)
            await SignIn(email, password: "wrong password");
        var right = await SignIn(email);

        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
    }

    [Fact]
    public async Task ARightPassword_DoesNotClearTheCount()
    {
        var email = NewEmail();
        await _api.RegisterConfirmedAsync(email);
        var (key, _) = await _api.EnableTwoFactorAsync(email);

        await SignIn(email, code: WrongCode(key));
        await SignIn(email, code: WrongCode(key));
        // The password alone is right, but the sign-in is not finished.
        await SignIn(email);
        var next = await SignIn(email, code: WrongCode(key));

        Assert.Equal(HttpStatusCode.TooManyRequests, next.StatusCode);
    }
}
