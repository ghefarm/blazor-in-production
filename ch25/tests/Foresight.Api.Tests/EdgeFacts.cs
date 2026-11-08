using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// What ASP.NET Core's forwarded headers do with Cloudflare's header, on a
/// small app of their own. 198.51.100.0/24 stands in for Cloudflare.
/// </summary>
[Trait("Chapter", "23")]
public sealed class EdgeFacts
{
    private const string Edge = "198.51.100.0/24";
    private const string Header = "CF-Connecting-IP";

    [Fact]
    public async Task TheHeader_IsBelievedOnlyFromTheEdge()
    {
        await using var app = await StartAsync();

        var through = await AddressAsync(app, "198.51.100.7", "203.0.113.9");
        var around = await AddressAsync(app, "192.0.2.50", "203.0.113.9");

        // Through the edge, the header names the visitor. Around the edge,
        // it is ignored, and the connection's own address counts.
        Assert.Equal("203.0.113.9", through);
        Assert.Equal("192.0.2.50", around);
    }

    [Fact]
    public async Task WithBothListsEmpty_TheHeaderIsBelievedFromAnyone()
    {
        await using var app = await StartAsync(edge: null);

        // Empty lists do not mean "trust nobody". They mean "check
        // nothing", so a stranger's header names the visitor.
        Assert.Equal("203.0.113.9",
            await AddressAsync(app, "192.0.2.50", "203.0.113.9"));
    }

    private static Task<WebApplication> StartAsync(string? edge = Edge) =>
        TestApp.StartAsync(
            services => services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                o.ForwardedForHeaderName = Header;
                o.KnownProxies.Clear();
                o.KnownIPNetworks.Clear();
                if (edge is not null)
                    o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(edge));
            }),
            app => app.UseForwardedHeaders());

    /// <summary>The address the app sees for a request from one address
    /// that names another in the header.</summary>
    private static async Task<string> AddressAsync(
        WebApplication app, string from, string says)
    {
        var server = app.GetTestServer();
        using var client = new HttpClient(server.CreateHandler(c =>
            c.Connection.RemoteIpAddress = IPAddress.Parse(from)))
        {
            BaseAddress = server.BaseAddress,
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(Header, says);
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
