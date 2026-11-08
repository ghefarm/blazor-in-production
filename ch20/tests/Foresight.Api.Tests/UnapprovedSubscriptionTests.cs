using System.Net.Http.Json;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>A subscription that PayPal created, but nobody approved.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "8")]
public sealed class UnapprovedSubscriptionTests
{
    private readonly ApiFactory _api;

    public UnapprovedSubscriptionTests(ApiFactory api)
    {
        _api = api;
    }

    private ISubscriptionStore Store =>
        _api.Services.GetRequiredService<ISubscriptionStore>();

    private static string NewEmail() =>
        $"user-{Guid.NewGuid():N}@example.test";

    private static string NewId() =>
        $"I-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    [Fact]
    public async Task ASubscriptionNobodyApproved_UnlocksNothing()
    {
        var email = NewEmail();
        await _api.RegisterConfirmedAsync(email);
        await Store.UpsertAsync(email, new PayPalSubscription(NewId(),
            PayPalStatus.ApprovalPending, "P-MONTHLY", email, null));

        using var client = await _api.SignedInClientAsync(email);
        var me = await client.GetFromJsonAsync<SubscriptionStatusDto>(
            "api/subscription/me");

        Assert.Equal(PayPalStatus.ApprovalPending, me!.Status);
        Assert.False(me.HasPremium);
    }
}
