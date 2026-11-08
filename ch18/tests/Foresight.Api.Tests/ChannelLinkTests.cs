using System.Net.Http.Json;
using Foresight.Api.Data;
using Foresight.Api.Defenses;
using Foresight.Billing;
using Foresight.Shared;
using Foresight.Telegram;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// One link per subscriber, a bot that checks it, and nobody left in the
/// channel whose premium has ended.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "17")]
public sealed class ChannelLinkTests
{
    private const string Key = "a-scheduler-key-of-at-least-32-characters";

    private readonly ApiFactory _api;
    private readonly FakeChannelMembers _bot = new();
    private readonly WebApplicationFactory<Program> _host;

    public ChannelLinkTests(ApiFactory api)
    {
        _api = api;
        // The API with the fake bot, the two channels and a scheduler key.
        _host = ChannelHost.With(api, new FakeTelegram(), s =>
        {
            s.AddSingleton<IChannelMembers>(_bot);
            s.Configure<TrustedCallerOptions>(o => o.SchedulerKey = Key);
        });
    }

    private ISubscriptionStore Store() =>
        _host.Services.GetRequiredService<ISubscriptionStore>();

    // A confirmed account with an active subscription, paid for 20 days.
    private async Task<(string Email, string Id)> SubscriberAsync()
    {
        var email = $"member-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        var sub = new PayPalSubscription(
            $"I-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
            PayPalStatus.Active, "P-MONTHLY", email,
            DateTime.UtcNow.AddDays(20));
        await Store().UpsertAsync(email, sub);
        return (email, sub.Id);
    }

    private async Task<string> LinkAsync(string email)
    {
        using var client = await ChannelHost.SignedInAsync(_host, email);
        var answer = await client.GetAsync("api/premium-channel/link");
        answer.EnsureSuccessStatusCode();
        return (await answer.Content
            .ReadFromJsonAsync<PremiumChannelLink>())!.Link;
    }

    // What the scheduler asks for on every real tick.
    private async Task SyncAsync()
    {
        using var scheduler = _host.CreateClient();
        scheduler.DefaultRequestHeaders.Add(ApiHeaders.SchedulerKey, Key);
        var answer = await scheduler.PostAsync(
            "api/premium-channel/sync", null);
        answer.EnsureSuccessStatusCode();
    }

    private async Task<ChannelLinkRow?> RowAsync(string link)
    {
        await using var db = await _host.Services
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        return await db.ChannelLinks.SingleOrDefaultAsync(l => l.Link == link);
    }

    [Fact]
    public async Task TheSubscriber_IsLetIn_AForwardedLinkIsNot()
    {
        var (email, _) = await SubscriberAsync();
        var link = await LinkAsync(email);
        _bot.Ask(userId: 1001, link);    // the subscriber
        _bot.Ask(userId: 1002, link);    // a friend with a forwarded copy

        await SyncAsync();
        _bot.Ask(userId: 1002, link);    // and the friend tries again
        await SyncAsync();

        Assert.Equal([1001], _bot.Approved);
        Assert.Equal([1002, 1002], _bot.Declined);
    }

    [Fact]
    public async Task ARefund_RemovesTheMember_OnTheNextSync()
    {
        var (email, id) = await SubscriberAsync();
        var link = await LinkAsync(email);
        _bot.Ask(userId: 2001, link);
        await SyncAsync();

        // The webhook of chapter 9 does this when PayPal reports a refund.
        await Store().RevokeAsync(id);
        await SyncAsync();

        Assert.Equal([2001], _bot.Approved);
        Assert.Equal([2001], _bot.Removed);
        Assert.Contains(link, _bot.Revoked);
        Assert.Null(await RowAsync(link));
    }
}
