using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Foresight.Api.Tests;

/// <summary>The key that signs every login, and what it opens.</summary>
[Collection(ApiCollection.Name)]
[Trait("Chapter", "21")]
public sealed class KeyInRepositoryTests
{
    // Admin only: a token whose signature is accepted but whose account is
    // not the admin gets 403, a token that is not accepted gets 401, and
    // nothing changes either way. That makes it a safe probe.
    private const string Probe = "api/trackrecord/refresh";

    private readonly ApiFactory _api;

    public KeyInRepositoryTests(ApiFactory api)
    {
        _api = api;
    }

    // A login token, signed with a key and nothing else: no account, no
    // password. This is all that someone who has read the key needs.
    private static string Forge(string key, string email) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Foresight.Api",
            Audience = "Foresight.Web",
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Email, email)]),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256),
        });

    // A key of the kind section 21.2 makes: 64 random bytes, as base64.
    private static string NewKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // The API started with a key of the test's choice. Program.cs reads the
    // key from the environment at its start, so the variable is set only
    // until the host has been built.
    private static WebApplicationFactory<Program> StartedWith(string key)
    {
        var before = Environment.GetEnvironmentVariable("Jwt__Key");
        Environment.SetEnvironmentVariable("Jwt__Key", key);
        try
        {
            var host = new WebApplicationFactory<Program>();
            host.CreateClient().Dispose();
            return host;
        }
        finally
        {
            Environment.SetEnvironmentVariable("Jwt__Key", before);
        }
    }

    private static Task<HttpResponseMessage> PostProbe(
        HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Probe);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task WhoeverHoldsTheKey_IsTheAdmin_WithNoPassword()
    {
        // The admin's account exists, as it does on every real host.
        await _api.EnsureAdminAsync();
        var key = NewKey();
        using var api = StartedWith(key);
        using var client = api.CreateClient();

        var response = await PostProbe(
            client, Forge(key, ApiFactory.AdminEmail));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Every settings file in the repository, every value whose name says
    // it is a secret: each one must be empty. The real values live on the
    // host, and in user secrets on a developer machine.
    [Fact]
    public void TheCommittedSettings_HoldNoSecret()
    {
        var src = Path.Combine(SolutionFolder(), "src");
        var files = Directory.GetFiles(
            src, "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin"));

        var found = files.SelectMany(file =>
            Secrets(JsonDocument.Parse(File.ReadAllText(file)).RootElement,
                Path.GetFileName(file)));

        Assert.Empty(found);
    }

    private static readonly string[] SecretNames =
        ["Key", "Secret", "Token", "Password"];

    // The paths of every non-empty value whose name ends in a secret word.
    private static IEnumerable<string> Secrets(JsonElement e, string path)
    {
        if (e.ValueKind != JsonValueKind.Object)
            yield break;
        foreach (var p in e.EnumerateObject())
        {
            var here = $"{path}:{p.Name}";
            if (p.Value.ValueKind == JsonValueKind.String
                && SecretNames.Any(n => p.Name.EndsWith(n,
                    StringComparison.OrdinalIgnoreCase))
                && p.Value.GetString() is { Length: > 0 })
                yield return here;
            foreach (var inner in Secrets(p.Value, here))
                yield return inner;
        }
    }

    private static string SolutionFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(
            Path.Combine(dir.FullName, "Foresight.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("No Foresight.slnx.");
    }

    [Fact]
    public async Task TheProbe_403WhileTheOldKeyWorks_401AfterTheRotation()
    {
        // An account that exists and is not the admin's. A made-up email
        // gets 401 whatever the key (chapter 7), and the probe would lie.
        var email = $"member-{Guid.NewGuid():N}@example.test";
        await _api.RegisterConfirmedAsync(email);
        var oldKey = NewKey();
        var member = Forge(oldKey, email);

        HttpStatusCode before, after;
        using (var api = StartedWith(oldKey))
        using (var client = api.CreateClient())
            before = (await PostProbe(client, member)).StatusCode;
        using (var api = StartedWith(NewKey()))
        using (var client = api.CreateClient())
            after = (await PostProbe(client, member)).StatusCode;

        Assert.Equal(HttpStatusCode.Forbidden, before);
        Assert.Equal(HttpStatusCode.Unauthorized, after);
    }
}
