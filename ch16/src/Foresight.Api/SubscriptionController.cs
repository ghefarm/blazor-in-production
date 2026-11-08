using Foresight.Api.Accounts;
using Foresight.Api.Data;
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
    private readonly ContractStore _contracts;
    private readonly TimeProvider _clock;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        SubscriptionManager subscriptions,
        PremiumAccess premium,
        ContractStore contracts,
        TimeProvider clock,
        ILogger<SubscriptionController> logger)
    {
        _subscriptions = subscriptions;
        _premium = premium;
        _contracts = contracts;
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

    /// <summary>The page reports the subscription PayPal approved.</summary>
    [HttpPost("claim")]
    [Authorize]
    public async Task<IActionResult> ClaimAsync(
        ClaimRequest request, CancellationToken ct = default)
    {
        ClaimOutcome outcome;
        try
        {
            outcome = await _subscriptions.ClaimAsync(
                User.GetEmail()!, request.SubscriptionId, ct);
        }
        catch (PayPalException ex)
        {
            // The payment may well have gone through, and the webhook will
            // record it. So the visitor is told to wait, not to pay again.
            _logger.LogError(ex, "Could not check a claimed subscription");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ErrorResponse(PayPalNotSure));
        }

        return outcome switch
        {
            ClaimOutcome.Recorded => Ok(await MeAsync(ct)),
            ClaimOutcome.NotAnId => BadRequest(new ErrorResponse(
                "That is not a PayPal subscription id.")),
            ClaimOutcome.NotFound => NotFound(new ErrorResponse(
                "PayPal does not know that subscription.")),
            ClaimOutcome.WrongPlan => BadRequest(new ErrorResponse(
                "That subscription is not for Foresight premium.")),
            _ => StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse(
                "That subscription belongs to another account.")),
        };
    }

    private const string PayPalNotSure =
        "PayPal could not confirm your subscription yet. If you have paid, "
        + "premium unlocks on its own within a few minutes.";

    /// <summary>The consent box was ticked, before PayPal opens.</summary>
    [HttpPost("consent")]
    [Authorize]
    public async Task<IActionResult> ConsentAsync(
        ConsentRequest request, CancellationToken ct = default)
    {
        // A page that shows an older text than the server's is refused, so
        // nobody is recorded as agreeing to words they did not see.
        if (request.TextVersion != PremiumTerms.ConsentVersion)
            return Conflict(new ErrorResponse(
                "The terms have changed. Please reload the page."));

        await _contracts.RecordConsentAsync(User.GetEmail()!, ct);
        return Ok();
    }

    /// <summary>The signed-in subscriber cancels on our own page.</summary>
    [HttpPost("cancel")]
    [Authorize]
    public async Task<IActionResult> CancelAsync(
        CancellationToken ct = default)
    {
        try
        {
            await _subscriptions.CancelAsync(User.GetEmail()!, ct);
        }
        catch (PayPalException ex)
        {
            _logger.LogError(ex, "Could not cancel a subscription at PayPal");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ErrorResponse("PayPal could not be reached, so nothing "
                    + "was cancelled. Please try again in a few minutes."));
        }
        return Ok(await MeAsync(ct));
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
