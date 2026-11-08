using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>Posting by hand: the tier picks the channel.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "13")]
public sealed class ChannelTierTests
{
    private readonly ApiFactory _api;

    public ChannelTierTests(ApiFactory api)
    {
        _api = api;
    }

    private async Task<HttpClient> AdminAsync(FakeTelegram telegram)
    {
        await _api.EnsureAdminAsync();
        return await ChannelHost.SignedInAsync(
            ChannelHost.With(_api, telegram), ApiFactory.AdminEmail);
    }

    [Fact]
    public async Task EachTier_GoesToItsOwnChannel()
    {
        var telegram = new FakeTelegram();
        using var admin = await AdminAsync(telegram);

        (await admin.PostAsJsonAsync("api/channels/post",
            new ChannelPost(Premium: false, "free text")))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("api/channels/post",
            new ChannelPost(Premium: true, "premium text")))
            .EnsureSuccessStatusCode();

        Assert.Equal(
            [(ChannelHost.Free, "free text"),
             (ChannelHost.Premium, "premium text")],
            telegram.Sent);
    }
}
