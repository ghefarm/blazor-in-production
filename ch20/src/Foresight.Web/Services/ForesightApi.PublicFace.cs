using Foresight.Shared;

namespace Foresight.Web.Services;

// The calls of the public pages: the poll and the track record.
public sealed partial class ForesightApi
{
    /// <summary>The poll, with the visitor's own vote when signed in.</summary>
    public Task<ApiResult<PollDto>> PollAsync(
        CancellationToken ct = default) =>
        SendAsync<PollDto>(HttpMethod.Get, "api/poll", null,
            withToken: true, ct);

    public Task<ApiResult<PollDto>> VoteAsync(
        string candidate, CancellationToken ct = default) =>
        SendAsync<PollDto>(HttpMethod.Put, "api/poll/vote",
            new VoteRequest(candidate), withToken: true, ct);

    /// <summary>One feed's track record. Public: no token is sent.</summary>
    public Task<ApiResult<TrackRecordDto>> TrackRecordAsync(
        string feed, CancellationToken ct = default) =>
        SendAsync<TrackRecordDto>(HttpMethod.Get,
            $"api/trackrecord/{Uri.EscapeDataString(feed)}", null,
            withToken: false, ct);
}
