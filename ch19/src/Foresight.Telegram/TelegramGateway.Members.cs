using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Foresight.Telegram;

/// <summary>
/// Someone asked to join a channel with an invite link. The bot decides.
/// </summary>
/// <param name="UpdateId">Telegram's number for this piece of news.</param>
public sealed record JoinRequest(
    int UpdateId, long ChatId, long UserId, string? Link);

/// <summary>
/// The Bot API calls that decide who is in the premium channel. Tests use
/// a fake.
/// </summary>
public interface IChannelMembers
{
    /// <summary>A new link that asks to join instead of joining.</summary>
    Task<string> CreateJoinLinkAsync(
        string name, CancellationToken ct = default);

    Task RevokeLinkAsync(string link, CancellationToken ct = default);

    /// <summary>
    /// The join requests Telegram holds for the bot, and the number of the
    /// last update it read, if there was one.
    /// </summary>
    Task<(IReadOnlyList<JoinRequest> Requests, int? Last)>
        ReadJoinRequestsAsync(CancellationToken ct = default);

    /// <summary>Tells Telegram that every update up to this one is done.
    /// </summary>
    Task MarkReadAsync(int last, CancellationToken ct = default);

    Task ApproveAsync(long userId, CancellationToken ct = default);

    Task DeclineAsync(long userId, CancellationToken ct = default);

    /// <summary>Out of the channel, free to come back with a new link.
    /// </summary>
    Task RemoveAsync(long userId, CancellationToken ct = default);
}

// The premium channel's door, through the same CallAsync as every post,
// so the token stays out of every message here too.
public sealed partial class TelegramGateway : IChannelMembers
{
    private static readonly UpdateType[] OnlyJoinRequests =
        [UpdateType.ChatJoinRequest];

    public Task<string> CreateJoinLinkAsync(
        string name, CancellationToken ct = default) =>
        CallAsync("createChatInviteLink", async bot =>
            (await bot.CreateChatInviteLink(Premium, name,
                createsJoinRequest: true, cancellationToken: ct))
            .InviteLink);

    public Task RevokeLinkAsync(
        string link, CancellationToken ct = default) =>
        CallAsync("revokeChatInviteLink",
            bot => bot.RevokeChatInviteLink(Premium, link, ct));

    public Task<(IReadOnlyList<JoinRequest> Requests, int? Last)>
        ReadJoinRequestsAsync(CancellationToken ct = default) =>
        CallAsync("getUpdates", async bot =>
        {
            // No offset: every update Telegram still holds for the bot.
            var updates = await bot.GetUpdates(timeout: 0,
                allowedUpdates: OnlyJoinRequests, cancellationToken: ct);
            IReadOnlyList<JoinRequest> requests = updates
                .Where(u => u.ChatJoinRequest is not null)
                .Select(u => new JoinRequest(u.Id,
                    u.ChatJoinRequest!.Chat.Id, u.ChatJoinRequest.From.Id,
                    u.ChatJoinRequest.InviteLink?.InviteLink))
                .ToList();
            int? last = updates.Length == 0 ? null : updates.Max(u => u.Id);
            return (requests, last);
        });

    // An update counts as read once getUpdates is asked for a later one.
    public Task MarkReadAsync(int last, CancellationToken ct = default) =>
        CallAsync("getUpdates", bot => bot.GetUpdates(offset: last + 1,
            limit: 1, timeout: 0, allowedUpdates: OnlyJoinRequests,
            cancellationToken: ct));

    public Task ApproveAsync(long userId, CancellationToken ct = default) =>
        CallAsync("approveChatJoinRequest",
            bot => bot.ApproveChatJoinRequest(Premium, userId, ct));

    public Task DeclineAsync(long userId, CancellationToken ct = default) =>
        CallAsync("declineChatJoinRequest",
            bot => bot.DeclineChatJoinRequest(Premium, userId, ct));

    // An unban without onlyIfBanned takes a member out of the channel and
    // leaves no ban behind, so a new subscription can bring them back.
    public Task RemoveAsync(long userId, CancellationToken ct = default) =>
        CallAsync("unbanChatMember", bot => bot.UnbanChatMember(
            Premium, userId, onlyIfBanned: false, ct));

    private string Premium => _options.PremiumChannel;

    private Task CallAsync(
        string method, Func<TelegramBotClient, Task> call) =>
        CallAsync(method, async bot =>
        {
            await call(bot);
            return true;
        });
}
