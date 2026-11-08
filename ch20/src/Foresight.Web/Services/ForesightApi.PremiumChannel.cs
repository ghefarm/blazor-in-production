using Foresight.Shared;

namespace Foresight.Web.Services;

public sealed partial class ForesightApi
{
    /// <summary>The signed-in subscriber's own link to the premium
    /// channel. The API refuses an account without premium.</summary>
    public Task<ApiResult<PremiumChannelLink>> PremiumChannelLinkAsync(
        CancellationToken ct = default) =>
        SendAsync<PremiumChannelLink>(HttpMethod.Get,
            "api/premium-channel/link", null, withToken: true, ct);
}
