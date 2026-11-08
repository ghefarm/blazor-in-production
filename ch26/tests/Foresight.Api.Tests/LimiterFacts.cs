using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// What ASP.NET Core's rate limiter and the in-memory test server do, on
/// a small app of their own.
/// </summary>
[Trait("Chapter", "22")]
public sealed class LimiterFacts
{
    [Fact]
    public async Task TheLimiter_CountsEachKeyAlone_AndRefusesWith503()
    {
        // Two requests a minute for each value of the header "Who".
        await using var app = await TestApp.StartAsync(
            services => services.AddRateLimiter(o => o.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(c =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        c.Request.Headers["Who"].ToString(),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 2,
                            Window = TimeSpan.FromMinutes(1),
                        }))),
            app => app.UseRateLimiter());
        using var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "ann"));
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "ann"));
        // Not 429: unless it is told otherwise, the limiter answers "503
        // Service Unavailable", as if the server were in trouble.
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            await AskAsync(client, "ann"));
        Assert.Equal(HttpStatusCode.OK, await AskAsync(client, "bob"));
    }

    private static async Task<HttpStatusCode> AskAsync(
        HttpClient client, string who)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Who", who);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

/// <summary>
/// A small app on the in-memory test server, for framework facts: the
/// services and middleware under test, and one endpoint that answers with
/// the caller's address as the app sees it.
/// </summary>
public static class TestApp
{
    public static async Task<WebApplication> StartAsync(
        Action<IServiceCollection> services, Action<WebApplication> pipeline)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        services(builder.Services);
        var app = builder.Build();
        pipeline(app);
        app.MapGet("/", (HttpContext c) =>
            c.Connection.RemoteIpAddress?.ToString() ?? "none");
        await app.StartAsync();
        return app;
    }
}
