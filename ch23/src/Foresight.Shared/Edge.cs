using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Foresight.Shared;

/// <summary>
/// The "Edge" section: the proxy in front of the app. With no ranges there
/// is no proxy, and the connection's own address is the visitor's.
/// </summary>
public sealed class EdgeOptions
{
    public const string SectionName = "Edge";

    // The header in which the proxy names the visitor. Cloudflare's.
    public string AddressHeader { get; set; } = "CF-Connecting-IP";

    // The proxy's own address ranges, as Cloudflare publishes them. Not a
    // secret, but they must be the current ones: a visitor who comes
    // through a missing range counts as the proxy's address.
    public string[] Ranges { get; set; } = [];
}

/// <summary>
/// Where a visitor's address comes from. The API and the website use the
/// same rule, from their own "Edge" section.
/// </summary>
public static class Edge
{
    /// <summary>Reads the "Edge" section into ASP.NET Core's forwarded
    /// headers, which UseForwardedHeaders applies.</summary>
    public static IServiceCollection AddEdge(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<EdgeOptions>(
            config.GetSection(EdgeOptions.SectionName));
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<EdgeOptions>>(
                (options, edge) => Apply(options, edge.Value));
        return services;
    }

    private static void Apply(ForwardedHeadersOptions options, EdgeOptions edge)
    {
        // Behind Cloudflare, every connection comes from Cloudflare, and
        // the visitor's address arrives in a header. Anyone can write that
        // header, so it is believed only on a connection from Cloudflare's
        // own ranges.
        options.ForwardedForHeaderName = edge.AddressHeader;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var range in edge.Ranges)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(range));

        // With both lists empty, the middleware checks nothing and believes
        // anyone. So no ranges means no proxy: the header is not read.
        options.ForwardedHeaders = edge.Ranges.Length > 0
            ? ForwardedHeaders.XForwardedFor
            : ForwardedHeaders.None;
    }
}
