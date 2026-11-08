using Foresight.Api.Accounts;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Foresight.Api;

/// <summary>Subscriptions: the account's own, and PayPal's reports.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SubscriptionController : ControllerBase
{
    // Far more than any real event, and far less than the 30 MB a stranger
    // could otherwise make the server read for one request.
    private const int MaxWebhookBytes = 64 * 1024;

    private readonly SubscriptionManager _subscriptions;
    private readonly PremiumAccess _premium;
    private readonly TimeProvider _clock;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        SubscriptionManager subscriptions,
        PremiumAccess premium,
        TimeProvider clock,
        ILogger<SubscriptionController> logger)
    {
        _subscriptions = subscriptions;
        _premium = premium;
        _clock = clock;
        _logger = logger;
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

    // PayPal calls this. There is no login, so the only proof is PayPal's
    // signature, and PayPal itself is asked to check it.
    [HttpPost("webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxWebhookBytes)]
    public async Task<IActionResult> WebhookAsync(
        CancellationToken ct = default)
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        try
        {
            var ours = await _subscriptions.HandleWebhookAsync(
                Request.Headers, body, ct);
            return ours ? Ok() : BadRequest();
        }
        catch (PayPalException ex)
        {
            // Anything but a 2xx makes PayPal send the event again later.
            _logger.LogError(ex, "Could not process a PayPal webhook");
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
