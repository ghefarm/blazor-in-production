using System.Net;
using Foresight.Api.Channels;
using Foresight.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Foresight.Api.Tests;

/// <summary>
/// A client from the HTTP client factory writes the address of every
/// request into the log. The bot token is part of that address, so the
/// Telegram client must write none.
/// </summary>
[Trait("Chapter", "17")]
public sealed class TokenLogTests
{
    private const string Token = "123456:SECRET-TOKEN";

    /// <summary>Every log line, whatever its category.</summary>
    private sealed class Lines : ILoggerProvider, ILogger
    {
        public List<(LogLevel Level, string Text)> All { get; } = new();

        public ILogger CreateLogger(string category) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(LogLevel level, EventId id, TState state,
            Exception? error, Func<TState, Exception?, string> format)
        {
            lock (All) All.Add((level, format(state, error)));
        }

        public void Dispose()
        {
        }
    }

    private static TelegramFacts.Wire Wire() => new(HttpStatusCode.OK,
        """{"ok":true,"result":{"message_id":1,"date":1762000000,"""
        + """ "chat":{"id":-1001234567890,"type":"channel"}}}""");

    [Fact]
    public async Task TheTelegramClient_WritesNoAddress()
    {
        var lines = new Lines();
        var services = new ServiceCollection();
        services.AddLogging(b => b
            .SetMinimumLevel(LogLevel.Trace).AddProvider(lines));
        // The API's own registrations, with a token.
        services.AddChannels(new ConfigurationBuilder()
            .AddInMemoryCollection([new("Telegram:BotToken", Token)])
            .Build());
        services.AddHttpClient(TelegramGateway.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(Wire);
        using var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<TelegramGateway>()
            .SendAsync("-1001234567890", "Hello");

        Assert.DoesNotContain(lines.All, line => line.Text.Contains(Token));
    }
}
