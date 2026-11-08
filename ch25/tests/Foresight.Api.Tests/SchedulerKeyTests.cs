using System.Net;

namespace Foresight.Api.Tests;

/// <summary>The scheduler's own way to the forecasts: its key, and no
/// account at all.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "15")]
public sealed class SchedulerKeyTests
{
    private readonly ApiFactory _api;

    public SchedulerKeyTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task WithoutTheKey_EvenTheAdminGets403()
    {
        await _api.EnsureAdminAsync();
        using var admin = await _api.SignedInClientAsync(ApiFactory.AdminEmail);

        var answer = await admin.GetAsync(
            "api/scheduler/forecasts/transit?day=2026-11-02");

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
    }
}
