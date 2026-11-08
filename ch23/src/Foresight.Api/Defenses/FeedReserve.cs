using Foresight.Api.Feeds;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Defenses;

/// <summary>
/// The last requests of the feed's month, kept for the scheduler. Without
/// a reserve, a busy day on the website could use up the month and leave
/// the channels with nothing to post.
/// </summary>
public sealed class FeedReserve
{
    private readonly TrustedCallers _callers;
    private readonly FeedMeter _meter;
    private readonly RateLimitOptions _options;

    public FeedReserve(TrustedCallers callers, FeedMeter meter,
        IOptions<RateLimitOptions> options)
    {
        _callers = callers;
        _meter = meter;
        _options = options.Value;
    }

    /// <summary>True when only the reserve is left and this caller is
    /// not the scheduler.</summary>
    public async Task<bool> IsClosedToAsync(
        HttpRequest request, CancellationToken ct = default) =>
        !_callers.IsScheduler(request)
        && await _meter.RemainingAsync(ct) <= _options.FeedReserve;
}
