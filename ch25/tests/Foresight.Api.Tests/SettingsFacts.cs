using Microsoft.AspNetCore.Builder;

namespace Foresight.Api.Tests;

/// <summary>
/// The order in which a host reads its settings. Each test builds a host
/// over a folder of its own and never starts it.
/// </summary>
[Trait("Chapter", "25")]
public sealed class SettingsFacts : IDisposable
{
    private readonly string _root =
        Directory.CreateTempSubdirectory("foresight-settings-").FullName;

    [Fact]
    public void TheLastFile_Wins_EvenWithAnEmptyValue()
    {
        Write("appsettings.json", """{"Mail":{"Password":"real"}}""");
        Write("appsettings.Production.json", """{"Mail":{"Password":""}}""");

        var settings = HostIn("Production").Configuration;

        // Not null, not "real": the empty value was read and it won.
        Assert.Equal("", settings["Mail:Password"]);
    }

    [Fact]
    public void AnEnvironmentVariable_WinsOverEveryFile()
    {
        // A name no other test uses: environment variables are shared by
        // the whole test process.
        var section = $"Fact{Guid.NewGuid():N}";
        Write("appsettings.json", $$$"""{"{{{section}}}":{"Key":"file"}}""");
        Write("appsettings.Production.json",
            $$$"""{"{{{section}}}":{"Key":""}}""");
        Environment.SetEnvironmentVariable($"{section}__Key", "host");
        try
        {
            var settings = HostIn("Production").Configuration;

            Assert.Equal("host", settings[$"{section}:Key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{section}__Key", null);
        }
    }

    private void Write(string name, string json) =>
        File.WriteAllText(Path.Combine(_root, name), json);

    private WebApplicationBuilder HostIn(string environment) =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = _root,
            EnvironmentName = environment,
        });

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
