using Foresight.Api.Data;
using Foresight.Api.Feeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Tests;

/// <summary>The feed's monthly limit, kept in the database.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "3")]
public sealed class FeedMeterTests
{
    private readonly ApiFactory _api;

    public FeedMeterTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task TheMonthlyLimit_SurvivesARestart()
    {
        // A month no other test touches, so the count starts at zero.
        var clock = new FixedClock(new DateTimeOffset(2099, 1, 15, 0, 0, 0,
            TimeSpan.Zero));
        var db = _api.Services
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var limit = Options.Create(new FeedOptions { MonthlyLimit = 2 });
        FeedMeter Meter() => new(db, limit, clock);

        await Meter().CountAsync();
        await Meter().CountAsync();

        // A new meter is what a restart gives: it reads the database.
        await Assert.ThrowsAsync<FeedLimitException>(
            () => Meter().CountAsync());
    }
}

/// <summary>A clock that always shows the same moment.</summary>
public sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now;
}
