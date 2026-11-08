using System.Net;
using System.Text;
using Foresight.Telegram;

namespace Foresight.Api.Tests;

/// <summary>
/// What the Telegram.Bot package sends, and what it makes of Telegram's
/// answers. Telegram itself is not called: a handler stands in for the
/// network and answers in the shape the Bot API documents.
/// </summary>
[Trait("Chapter", "13")]
public sealed class TelegramFacts
{
    private const string Token = "123456:SECRET-TOKEN";

    private const string SentOk = """
        {"ok":true,"result":{"message_id":1,"date":1762000000,
         "chat":{"id":-1001234567890,"type":"channel"},"text":"x"}}
        """;

    /// <summary>Keeps the request and answers with a fixed reply.</summary>
    internal sealed class Wire : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;

        public Wire(HttpStatusCode status, string json)
        {
            _status = status;
            _json = json;
        }

        public Uri? Address { get; private set; }
        public string Body { get; private set; } = "";
        public bool Down { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Address = request.RequestUri;
            Body = request.Content is null
                ? "" : await request.Content.ReadAsStringAsync(ct);
            if (Down)
                throw new HttpRequestException("No such host is known.");
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(
                    _json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static TelegramGateway Gateway(Wire wire) =>
        new(() => new HttpClient(wire),
            new TelegramOptions { BotToken = Token });

    [Fact]
    public async Task EveryCall_CarriesTheTokenInItsAddress()
    {
        var wire = new Wire(HttpStatusCode.OK, SentOk);

        await Gateway(wire).SendAsync("@foresight_free", "Hello");

        Assert.Equal(
            $"https://api.telegram.org/bot{Token}/sendMessage",
            wire.Address!.ToString());
        Assert.Contains("\"parse_mode\":\"Markdown\"", wire.Body);
    }

    [Fact]
    public async Task ARefusal_ComesBackInTelegramsOwnWords()
    {
        var wire = new Wire(HttpStatusCode.BadRequest,
            """{"ok":false,"error_code":400,"description":"""
            + "\"Bad Request: can't parse entities\"}");

        var refused = await Assert.ThrowsAsync<TelegramException>(
            () => Gateway(wire).SendAsync("@foresight_free", "*open"));

        Assert.True(refused.Answered);
        Assert.Equal("Telegram refused sendMessage: Bad Request: "
            + "can't parse entities", refused.Message);
    }
}
