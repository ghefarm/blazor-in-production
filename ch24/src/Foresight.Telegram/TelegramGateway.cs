using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace Foresight.Telegram;

/// <summary>Sends a message to a channel. Tests use a fake.</summary>
public interface ITelegramSender
{
    /// <exception cref="TelegramException">The message did not go out.
    /// </exception>
    Task SendAsync(string chat, string text, CancellationToken ct = default);
}

/// <summary>
/// Telegram refused a call or could not be reached. The message holds the
/// method and Telegram's own words, never the token.
/// </summary>
public sealed class TelegramException : Exception
{
    public TelegramException(string message, bool answered)
        : base(message)
    {
        Answered = answered;
    }

    /// <summary>
    /// True when Telegram answered and said no: nothing was posted. False
    /// when no answer came back: the message may or may not have arrived.
    /// </summary>
    public bool Answered { get; }
}

/// <summary>
/// The Bot API calls Foresight makes, through the Telegram.Bot package.
/// </summary>
public sealed partial class TelegramGateway : ITelegramSender
{
    public const string HttpClientName = "telegram";

    private readonly Func<HttpClient> _http;
    private readonly TelegramOptions _options;

    /// <param name="http">A client per call; the API passes one from its
    /// client factory, the scheduler its one client.</param>
    public TelegramGateway(Func<HttpClient> http, TelegramOptions options)
    {
        _http = http;
        _options = options;
    }

    public Task SendAsync(
        string chat, string text, CancellationToken ct = default) =>
        CallAsync("sendMessage", bot => bot.SendMessage(
            chat, text, ParseMode.Markdown, cancellationToken: ct));

    // The token is part of every address Telegram.Bot calls, so neither the
    // address nor an exception that could hold it is passed on: only the
    // method name and Telegram's own description.
    private async Task<T> CallAsync<T>(
        string method, Func<TelegramBotClient, Task<T>> call)
    {
        var bot = new TelegramBotClient(_options.BotToken, _http());
        try
        {
            return await call(bot);
        }
        catch (ApiRequestException ex)
        {
            throw new TelegramException(
                $"Telegram refused {method}: {ex.Message}", answered: true);
        }
        catch (Exception ex)
            when (ex is RequestException or HttpRequestException)
        {
            throw new TelegramException(
                $"Telegram could not be reached for {method}.",
                answered: false);
        }
    }
}
