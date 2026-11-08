using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Foresight.Api;

/// <summary>The public part of the PayPal settings, for the page.</summary>
[ApiController]
[Route("api/subscription/plans")]
public sealed class PlansController : ControllerBase
{
    private readonly PayPalOptions _paypal;

    public PlansController(IOptions<PayPalOptions> paypal)
    {
        _paypal = paypal.Value;
    }

    // The client id comes from the same settings as the server's own PayPal
    // calls, so the button and the server can never be on different PayPal
    // modes, the sandbox and the live one.
    [HttpGet]
    public PlansDto Get() =>
        new(_paypal.ClientId, _paypal.MonthlyPlanId, _paypal.YearlyPlanId);
}
