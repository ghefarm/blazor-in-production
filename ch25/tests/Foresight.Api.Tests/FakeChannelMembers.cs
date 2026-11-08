using Foresight.Telegram;

namespace Foresight.Api.Tests;

/// <summary>
/// The premium channel's door for the API tests: it makes links, holds
/// join requests until they are read, and keeps every answer. Or it is
/// down, with the words a failed call gets.
/// </summary>
internal sealed class FakeChannelMembers : IChannelMembers
{
    private readonly List<JoinRequest> _waiting = new();
    private int _nextUpdate = 1;

    public List<string> Links { get; } = new();
    public List<string> Revoked { get; } = new();
    public List<long> Approved { get; } = new();
    public List<long> Declined { get; } = new();
    public List<long> Removed { get; } = new();
    public bool Down { get; set; }

    /// <summary>A Telegram user opens a link and asks to join.</summary>
    public void Ask(long userId, string link,
        string chat = ChannelHost.Premium) =>
        _waiting.Add(new JoinRequest(
            _nextUpdate++, long.Parse(chat), userId, link));

    public Task<string> CreateJoinLinkAsync(
        string name, CancellationToken ct = default)
    {
        ThrowIfDown("createChatInviteLink");
        var link = $"https://t.me/+{Guid.NewGuid():N}"[..30];
        Links.Add(link);
        return Task.FromResult(link);
    }

    public Task RevokeLinkAsync(string link, CancellationToken ct = default)
    {
        ThrowIfDown("revokeChatInviteLink");
        Revoked.Add(link);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<JoinRequest> Requests, int? Last)>
        ReadJoinRequestsAsync(CancellationToken ct = default)
    {
        ThrowIfDown("getUpdates");
        IReadOnlyList<JoinRequest> copy = _waiting.ToList();
        int? last = copy.Count == 0 ? null : copy.Max(r => r.UpdateId);
        return Task.FromResult((copy, last));
    }

    public Task MarkReadAsync(int last, CancellationToken ct = default)
    {
        ThrowIfDown("getUpdates");
        _waiting.RemoveAll(r => r.UpdateId <= last);
        return Task.CompletedTask;
    }

    public Task ApproveAsync(long userId, CancellationToken ct = default) =>
        Note(Approved, userId, "approveChatJoinRequest");

    public Task DeclineAsync(long userId, CancellationToken ct = default) =>
        Note(Declined, userId, "declineChatJoinRequest");

    public Task RemoveAsync(long userId, CancellationToken ct = default) =>
        Note(Removed, userId, "unbanChatMember");

    private Task Note(List<long> list, long userId, string method)
    {
        ThrowIfDown(method);
        list.Add(userId);
        return Task.CompletedTask;
    }

    private void ThrowIfDown(string method)
    {
        if (Down)
            throw new TelegramException(
                $"Telegram could not be reached for {method}.",
                answered: false);
    }
}
