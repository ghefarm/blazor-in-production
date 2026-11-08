using System.Net;
using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>An account with a second factor needs it to be deleted.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "11")]
public sealed class DeleteTwoFactorTests
{
    private const string Url = "api/auth/delete-account";

    private readonly ApiFactory _api;

    public DeleteTwoFactorTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task WithTwoFactor_ThePasswordAloneDeletesNothing()
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        var (key, _) = await _api.EnableTwoFactorAsync(email);
        using var client = _api.CreateClient();

        var withoutCode = await client.PostAsJsonAsync(Url,
            new LoginRequest(email, ApiFactory.Password));
        var withCode = await client.PostAsJsonAsync(Url,
            new LoginRequest(email, ApiFactory.Password, Totp.Code(key)));

        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
    }
}
