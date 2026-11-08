using System.Net;
using System.Net.Http.Json;
using Foresight.Api.Data;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>The incident: a deletion that stops after PayPal.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "11")]
public sealed class HalfDeletedTests
{
    private const string Url = "api/auth/delete-account";

    private readonly ApiFactory _api;

    public HalfDeletedTests(ApiFactory api)
    {
        _api = api;
    }

    // The API with PayPal's stub, and a store whose first forget fails.
    private WebApplicationFactory<Program> WithForgetFailingOnce(
        StubPayPal paypal) =>
        _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IPayPalClient>(paypal);
            s.AddSingleton<ISubscriptionStore>(sp => new FailsOnce(
                ActivatorUtilities.CreateInstance<SubscriptionStore>(sp)));
        }));

    // A registered account with an active monthly subscription.
    private async Task<string> SubscriberAsync(
        WebApplicationFactory<Program> host, StubPayPal paypal)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        var id = $"I-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var sub = new PayPalSubscription(id, PayPalStatus.Active,
            "P-MONTHLY", email, DateTime.UtcNow.AddDays(20));
        paypal.Set(sub);
        await host.Services.GetRequiredService<ISubscriptionStore>()
            .UpsertAsync(email, sub);
        return email;
    }

    private static Task<HttpResponseMessage> Delete(
        HttpClient client, string email) =>
        client.PostAsJsonAsync(Url,
            new LoginRequest(email, ApiFactory.Password));

    private static Task<HttpResponseMessage> Login(
        HttpClient client, string email) =>
        client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));

    [Fact]
    public async Task AFailureAfterPayPal_SaysSo_AndASecondTryFinishes()
    {
        var paypal = new StubPayPal();
        using var host = WithForgetFailingOnce(paypal);
        var email = await SubscriberAsync(host, paypal);
        using var client = host.CreateClient();

        var first = await Delete(client, email);

        // Not "wrong password": the server says that it failed.
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal("Your account could not be deleted just now. "
            + "Please try again in a few minutes.",
            await ApiError.ReadAsync(first));

        // PayPal was told, the account is still there, and the same
        // request finishes the job.
        var second = await Delete(client, email);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Login(client, email)).StatusCode);
    }

    /// <summary>The real store, except that the first forget fails.</summary>
    private sealed class FailsOnce : ISubscriptionStore
    {
        private readonly ISubscriptionStore _inner;
        private bool _failed;

        public FailsOnce(ISubscriptionStore inner)
        {
            _inner = inner;
        }

        public Task ForgetAsync(string owner, CancellationToken ct = default)
        {
            if (_failed)
                return _inner.ForgetAsync(owner, ct);
            _failed = true;
            // The message in the server's log on the day of the incident.
            throw new IOException("attempt to write a readonly database");
        }

        // Everything else is the real store.
        public Task ForgetOneAsync(string id,
            CancellationToken ct = default) => _inner.ForgetOneAsync(id, ct);

        public Task UpsertAsync(string owner, PayPalSubscription s,
            CancellationToken ct = default) =>
            _inner.UpsertAsync(owner, s, ct);

        public Task RevokeAsync(string id, CancellationToken ct = default) =>
            _inner.RevokeAsync(id, ct);

        public Task<SubscriptionRecord?> FindAsync(string id,
            CancellationToken ct = default) => _inner.FindAsync(id, ct);

        public Task<SubscriptionRecord?> FindCurrentAsync(string owner,
            CancellationToken ct = default) =>
            _inner.FindCurrentAsync(owner, ct);

        public Task TouchAsync(string id, CancellationToken ct = default) =>
            _inner.TouchAsync(id, ct);

        public Task<IReadOnlyList<SubscriptionRecord>> FindAllAsync(
            string owner, CancellationToken ct = default) =>
            _inner.FindAllAsync(owner, ct);
    }
}
