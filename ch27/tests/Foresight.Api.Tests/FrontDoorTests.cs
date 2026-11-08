using System.Net;
using System.Net.Http.Json;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// The API's limits behind its edge. 198.51.100.0/24 stands in for
/// Cloudflare's ranges, as if it were set in "Edge:Ranges".
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "23")]
public sealed class FrontDoorTests
{
    private readonly ApiFactory _api;

    public FrontDoorTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task ANewHeaderPerRequest_GetsNoNewBucket_AroundTheEdge()
    {
        using var api = BehindTheEdge();
        using var stranger = From(api, "192.0.2.66");

        // Ten questions, each naming a new visitor in Cloudflare's header.
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK,
                (await AskAsync(stranger, $"203.0.113.{i}")).StatusCode);
        using var refused = await AskAsync(stranger, "203.0.113.99");

        // The header was never believed: all eleven came from one address.
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    private WebApplicationFactory<Program> BehindTheEdge() =>
        VisitorLimitTests.Shipped(_api, s =>
            s.Configure<EdgeOptions>(o => o.Ranges = ["198.51.100.0/24"]));

    // A client whose connection comes from the given address. The
    // in-memory server would give it none (EdgeFacts).
    private static HttpClient From(
        WebApplicationFactory<Program> api, string address) =>
        new(api.Server.CreateHandler(c =>
            c.Connection.RemoteIpAddress = IPAddress.Parse(address)))
        {
            BaseAddress = api.Server.BaseAddress,
        };

    private static Task<HttpResponseMessage> AskAsync(
        HttpClient client, string visitor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/what-if")
        {
            Content = JsonContent.Create(new WhatIfRequest("weather",
                VisitorLimitTests.EventId, 50)),
        };
        request.Headers.Add("CF-Connecting-IP", visitor);
        return client.SendAsync(request);
    }
}
