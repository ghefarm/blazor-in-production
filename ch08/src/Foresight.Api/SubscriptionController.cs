using Foresight.Api.Accounts;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api;

/// <summary>Subscriptions: the account's own.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SubscriptionController : ControllerBase
{
    private readonly PremiumAccess _premium;
    private readonly TimeProvider _clock;

    public SubscriptionController(PremiumAccess premium, TimeProvider clock)
    {
        _premium = premium;
        _clock = clock;
    }

    /// <summary>The signed-in account's subscription.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<SubscriptionStatusDto> MeAsync(
        CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var current = await _premium.CurrentAsync(User.GetEmail()!, ct);
        return new SubscriptionStatusDto(current?.Status,
            current?.IsEntitled(now) ?? false, current?.PremiumUntil(now));
    }
}
