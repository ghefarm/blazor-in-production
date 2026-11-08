using Foresight.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Web.Tests;

/// <summary>
/// The fix of chapter 5: the circuit's own API service attaches the token,
/// so each visitor's calls carry that visitor's token and no other.
/// </summary>
[Trait("Chapter", "5")]
public sealed class CircuitTokenTests
{
    private readonly RecordingApi _api = new();

    // The website's registrations, with no message handler at all.
    private ServiceProvider App()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("api",
                c => c.BaseAddress = new Uri("http://api.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => _api);
        services.AddScoped<CircuitTokenStore>();
        services.AddScoped<ForesightApi>();
        // The visitor's address (chapter 22); with no request it is empty.
        services.AddHttpContextAccessor();
        services.AddScoped<VisitorAddress>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EachCircuit_SendsItsOwnToken()
    {
        using var app = App();

        await VisitAsync(app, "ann-token");
        await VisitAsync(app, "bob-token");

        Assert.Equal(["Bearer ann-token", "Bearer bob-token"],
            _api.Authorizations);
    }

    private static async Task VisitAsync(ServiceProvider app, string token)
    {
        using var circuit = app.CreateScope();
        circuit.ServiceProvider.GetRequiredService<CircuitTokenStore>()
            .SignIn(token, $"{token}@example.test");
        await circuit.ServiceProvider.GetRequiredService<ForesightApi>()
            .ForecastsAsync("weather");
    }
}
