using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Foresight.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// The real API with its own data folder. Settings come from environment
/// variables, because Program.cs reads some of them before the test host
/// could change them.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.test";
    public const string Password = "a long test password";

    private readonly string _dataFolder = Path.Combine(
        Path.GetTempPath(), "foresight-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        Set("DataFolder", _dataFolder);
        Set("Jwt__Key",
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        Set("AdminEmail", AdminEmail);
        Set("FrontendUrl", "https://web.example.test");
        Set("Feed__MonthlyLimit", "100000");
        Set("PayPal__MonthlyPlanId", "P-MONTHLY");
        Set("PayPal__YearlyPlanId", "P-YEARLY");
    }

    public string MailFolder => Path.Combine(_dataFolder, "mail");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Production hashes cost a lot of time on purpose; tests need many.
        builder.ConfigureTestServices(s => s.Configure<PasswordHasherOptions>(
            o => o.IterationCount = 1000));

    /// <summary>Registers an account and opens the link in its mail.</summary>
    public async Task RegisterConfirmedAsync(string email)
    {
        using var client = CreateClient();
        var registered = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, Password));
        registered.EnsureSuccessStatusCode();

        var confirmed = await client.GetAsync(ConfirmPathFor(email));
        confirmed.EnsureSuccessStatusCode();
    }

    /// <summary>The admin account exists, confirmed, whichever test runs
    /// first.</summary>
    public async Task EnsureAdminAsync()
    {
        using var client = CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(AdminEmail, Password));
        if (!login.IsSuccessStatusCode)
            await RegisterConfirmedAsync(AdminEmail);
    }

    /// <summary>The API path of the link in the newest mail.</summary>
    public string ConfirmPathFor(string email)
    {
        var mail = Directory.GetFiles(MailFolder, "*.eml")
            .Order()
            .Select(File.ReadAllText)
            .Last(text => text.StartsWith($"To: {email}"));
        var link = System.Net.WebUtility.HtmlDecode(
            Regex.Match(mail, "href=\"([^\"]+)\"").Groups[1].Value);
        return "api/auth/confirm-email" + new Uri(link).Query;
    }

    public async Task<HttpClient> SignedInClientAsync(string email)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    /// <summary>
    /// Switches two-factor sign-in on for a confirmed account, as its owner
    /// would, and returns the key and the ten recovery codes.
    /// </summary>
    public async Task<(string Key, IReadOnlyList<string> Codes)>
        EnableTwoFactorAsync(string email)
    {
        using var client = await SignedInClientAsync(email);
        var setup = await client.PostAsync("api/auth/2fa/setup", null);
        var key = (await setup.Content
            .ReadFromJsonAsync<TwoFactorSetup>())!.Key;
        var enabled = await client.PostAsJsonAsync(
            "api/auth/2fa/enable", new TwoFactorCode(Totp.Code(key)));
        enabled.EnsureSuccessStatusCode();
        var codes = (await enabled.Content
            .ReadFromJsonAsync<RecoveryCodes>())!.Codes;
        return (key, codes);
    }

    private static void Set(string name, string value) =>
        Environment.SetEnvironmentVariable(name, value);
}

/// <summary>One API for every test class, started once.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
