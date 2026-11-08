using Foresight.Shared;

namespace Foresight.Web.Services;

/// <summary>
/// The admin's calls. The API decides who the admin is; a page that calls
/// these without the admin's token gets the API's 403 sentence back.
/// </summary>
public sealed partial class ForesightApi
{
    /// <summary>Ok only for the admin's token.</summary>
    public Task<ApiResult<bool>> AdminCheckAsync(
        CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Get, "api/admin/me", null,
            withToken: true, ct);

    public Task<ApiResult<PostingSwitch>> PostingAsync(
        CancellationToken ct = default) =>
        SendAsync<PostingSwitch>(HttpMethod.Get, "api/scheduler/posting",
            null, withToken: true, ct);

    public Task<ApiResult<PostingSwitch>> SetPostingAsync(
        bool on, CancellationToken ct = default) =>
        SendAsync<PostingSwitch>(HttpMethod.Post, "api/scheduler/posting",
            new PostingSwitch(on), withToken: true, ct);

    public Task<ApiResult<List<ChannelDraft>>> ChannelDraftsAsync(
        string feed, CancellationToken ct = default) =>
        SendAsync<List<ChannelDraft>>(HttpMethod.Get,
            $"api/channels/drafts/{Uri.EscapeDataString(feed)}", null,
            withToken: true, ct);

    public Task<ApiResult<bool>> PostToChannelAsync(
        ChannelPost post, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Post, "api/channels/post", post,
            withToken: true, ct);

    public Task<ApiResult<List<AdminAccount>>> AccountsAsync(
        CancellationToken ct = default) =>
        SendAsync<List<AdminAccount>>(HttpMethod.Get, "api/admin/accounts",
            null, withToken: true, ct);

    public Task<ApiResult<AdminMessage>> AdminDeleteAsync(
        string email, CancellationToken ct = default) =>
        SendAsync<AdminMessage>(HttpMethod.Post, "api/admin/accounts/delete",
            new AdminAccountRequest(email), withToken: true, ct);

    public Task<ApiResult<AdminMessage>> AdminTwoFactorOffAsync(
        string email, CancellationToken ct = default) =>
        SendAsync<AdminMessage>(HttpMethod.Post,
            "api/admin/accounts/two-factor-off",
            new AdminAccountRequest(email), withToken: true, ct);
}
