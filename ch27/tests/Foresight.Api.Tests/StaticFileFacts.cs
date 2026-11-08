using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Foresight.Api.Tests;

/// <summary>A framework fact about serving files from wwwroot.</summary>
[Trait("Chapter", "12")]
public sealed class StaticFileFacts
{
    [Fact]
    public async Task UseStaticFiles_RefusesAFileWithoutAnExtension()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var folder = Directory.CreateDirectory(
            Path.Combine(root, ".well-known")).FullName;
        const string Name = "apple-developer-merchantid-domain-association";
        File.WriteAllText(Path.Combine(folder, Name), "from PayPal");
        File.WriteAllText(Path.Combine(folder, "copy.txt"), "from PayPal");

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { WebRootPath = root });
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseStaticFiles();
        await app.StartAsync();
        using var client = app.GetTestClient();

        // The same bytes: served with an extension, refused without one.
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/.well-known/copy.txt")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/.well-known/{Name}")).StatusCode);
    }
}
