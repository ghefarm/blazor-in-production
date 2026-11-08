using System.Diagnostics;
using System.Net;
using System.Text;
using Foresight.Telegram;

namespace Foresight.Api.Tests;

/// <summary>
/// What the Telegram.Bot package does when Telegram says "too many
/// requests". The answers are written in the shape the Bot API documents;
/// Telegram itself is not called.
/// </summary>
[Trait("Chapter", "16")]
public sealed class TelegramRetryFacts
{
    private const string SentOk = """
        {"ok":true,"result":{"message_id":1,"date":1762000000,
         "chat":{"id":-1001234567890,"type":"channel"},"text":"x"}}
        """;

    private static string TooMany(int seconds) =>
        """{"ok":false,"error_code":429,"description":"""
        + $"\"Too Many Requests: retry after {seconds}\","
        + $"\"parameters\":{{\"retry_after\":{seconds}}}}}";

    /// <summary>Answers with the given replies, one per request; the
    /// last one again and again.</summary>
    private sealed class Replies : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _replies;

        public Replies(params (HttpStatusCode, string)[] replies)
        {
            _replies = new(replies);
        }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            var (status, json) = _replies.Count > 1
                ? _replies.Dequeue() : _replies.Peek();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static TelegramGateway Gateway(Replies replies) =>
        new(() => new HttpClient(replies),
            new TelegramOptions { BotToken = "123456:SECRET-TOKEN" });

    [Fact]
    public async Task AShortWait_IsWaitedOut_ByThePackage()
    {
        var wire = new Replies(
            (HttpStatusCode.TooManyRequests, TooMany(1)),
            (HttpStatusCode.OK, SentOk));
        var clock = Stopwatch.StartNew();

        await Gateway(wire).SendAsync("@foresight_free", "Hello");

        // No exception: Telegram.Bot waited the second that Telegram
        // asked for, and sent the message again.
        Assert.Equal(2, wire.Requests);
        Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(0.9));
    }
}
