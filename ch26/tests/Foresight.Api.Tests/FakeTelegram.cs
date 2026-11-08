using System.Net.Http.Headers;
using System.Net.Http.Json;
using Foresight.Shared;
using Foresight.Telegram;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foresight.Api.Tests;

/// <summary>
/// Telegram for the API tests: it keeps what it was sent, or it refuses
/// with the words Telegram uses.
/// </summary>
internal sealed class FakeTelegram : ITelegramSender
{
    public List<(string Chat, string Text)> Sent { get; } = new();

    /// <summary>Telegram's description for the next sends, if any.</summary>
    public string? Refuse { get; set; }

    public Task SendAsync(
        string chat, string text, CancellationToken ct = default)
    {
        if (Refuse is { } reason)
            throw new TelegramException(
                $"Telegram refused sendMessage: {reason}", answered: true);
        Sent.Add((chat, text));
        return Task.CompletedTask;
    }
}

/// <summary>The API of Part IV in a test: the fake and two channels.</summary>
internal static class ChannelHost
{
    public const string Free = "@foresight_free";
    public const string Premium = "-1001234567890";

    public static WebApplicationFactory<Program> With(
        ApiFactory api, FakeTelegram telegram,
        Action<IServiceCollection>? more = null) =>
        api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<ITelegramSender>(telegram);
            s.Configure<TelegramOptions>(o =>
            {
                o.BotToken = "123456:TEST-TOKEN";
                o.FreeChannel = Free;
                o.PremiumChannel = Premium;
                o.TimeZone = "Europe/Berlin";
            });
            more?.Invoke(s);
        }));

    /// <summary>A client of this host, signed in as the account.</summary>
    public static async Task<HttpClient> SignedInAsync(
        WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync("api/auth/login",
            new LoginRequest(email, ApiFactory.Password));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }
}
