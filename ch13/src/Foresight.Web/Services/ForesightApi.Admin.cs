using Foresight.Shared;

namespace Foresight.Web.Services;

/// <summary>
/// The admin's calls. The API decides who the admin is; a page that calls
/// these without the admin's token gets the API's 403 sentence back.
/// </summary>
public sealed partial class ForesightApi
{
    public Task<ApiResult<List<ChannelDraft>>> ChannelDraftsAsync(
        string feed, CancellationToken ct = default) =>
        SendAsync<List<ChannelDraft>>(HttpMethod.Get,
            $"api/channels/drafts/{Uri.EscapeDataString(feed)}", null,
            withToken: true, ct);

    public Task<ApiResult<bool>> PostToChannelAsync(
        ChannelPost post, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Post, "api/channels/post", post,
            withToken: true, ct);
}
