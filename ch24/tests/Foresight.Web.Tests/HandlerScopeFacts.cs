using Foresight.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Foresight.Web.Tests;

/// <summary>
/// Framework facts: where IHttpClientFactory gets the services of a message
/// handler, and how long it keeps the handler.
/// </summary>
[Trait("Chapter", "5")]
public sealed class HandlerScopeFacts
{
    private readonly RecordingApi _api = new();
    private readonly List<CircuitTokenStore> _handlerStores = [];

    // The website's services with the wrong-way handler on the "api" client.
    // Each handler the factory builds reports the token store it was given.
    private ServiceProvider App()
    {
        var services = new ServiceCollection();
        services.AddScoped<CircuitTokenStore>();
        services.AddTransient(sp =>
        {
            var store = sp.GetRequiredService<CircuitTokenStore>();
            lock (_handlerStores)
                _handlerStores.Add(store);
            return new TokenHandler(store);
        });
        services.AddHttpClient("api",
                c => c.BaseAddress = new Uri("http://api.test/"))
            .AddHttpMessageHandler<TokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => _api);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheHandler_GetsAStoreOfItsOwn_NotTheCircuits()
    {
        using var app = App();

        // Blazor Server gives every circuit a DI scope of its own. Here one
        // scope plays the circuit of a visitor who has signed in.
        using var circuit = app.CreateScope();
        var store = circuit.ServiceProvider
            .GetRequiredService<CircuitTokenStore>();
        store.SignIn("ann-token", "ann@example.test");

        var client = circuit.ServiceProvider
            .GetRequiredService<IHttpClientFactory>().CreateClient("api");
        await client.GetAsync("api/forecasts/weather");

        // The handler was given another CircuitTokenStore, an empty one,
        // so the request left with no token.
        Assert.DoesNotContain(store, _handlerStores);
        Assert.Null(_api.LastAuthorization);
    }

    [Fact]
    public async Task TwoCircuits_ShareOneHandler()
    {
        using var app = App();

        await VisitAsync(app, "ann-token");
        await VisitAsync(app, "bob-token");

        // One handler for both visitors: the factory keeps the chain it
        // built and hands it to every client of that name, for two minutes
        // unless the app says otherwise.
        Assert.Single(_handlerStores);
        Assert.All(_api.Authorizations, Assert.Null);
        Assert.Equal(TimeSpan.FromMinutes(2), app
            .GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>()
            .Get("api").HandlerLifetime);
    }

    // One circuit: a scope, a sign-in, one call through the factory.
    private static async Task VisitAsync(ServiceProvider app, string token)
    {
        using var circuit = app.CreateScope();
        circuit.ServiceProvider.GetRequiredService<CircuitTokenStore>()
            .SignIn(token, $"{token}@example.test");
        var client = circuit.ServiceProvider
            .GetRequiredService<IHttpClientFactory>().CreateClient("api");
        await client.GetAsync("api/forecasts/weather");
    }
}
