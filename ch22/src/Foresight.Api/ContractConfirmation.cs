using System.Net;
using Foresight.Api.Data;
using Foresight.Api.Mail;
using Foresight.Billing;
using Microsoft.Extensions.Options;

namespace Foresight.Api;

/// <summary>
/// Confirms the contract by mail, once per subscription, when PayPal first
/// reports it active. The claim and the webhook both report it; the table's
/// key makes sure that only one of them sends.
/// </summary>
public sealed class ContractConfirmation : ISubscriptionListener
{
    private readonly ContractStore _contracts;
    private readonly IMailSender _mail;
    private readonly PayPalOptions _paypal;
    private readonly ILogger<ContractConfirmation> _logger;

    public ContractConfirmation(
        ContractStore contracts,
        IMailSender mail,
        IOptions<PayPalOptions> paypal,
        ILogger<ContractConfirmation> logger)
    {
        _contracts = contracts;
        _mail = mail;
        _paypal = paypal.Value;
        _logger = logger;
    }

    public async Task RecordedAsync(
        string owner,
        PayPalSubscription subscription,
        CancellationToken ct = default)
    {
        // A contract exists once the subscription pays, not while it waits
        // for the buyer's approval.
        if (subscription.Status != PayPalStatus.Active)
            return;
        if (!await _contracts.TryMarkMailAsync(subscription.Id, owner, ct))
            return;

        try
        {
            var consent = await _contracts.LatestConsentAsync(owner, ct);
            await _mail.SendAsync(owner, "Your Foresight premium subscription",
                Body(subscription, consent), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The subscription is stored either way. Take the mark back, so
            // the next report of this subscription tries the mail again.
            await _contracts.UnmarkMailAsync(subscription.Id, ct);
            _logger.LogError(ex, "Could not send the contract mail for {Id}",
                subscription.Id);
        }
    }

    private string Body(PayPalSubscription s, ConsentRow? consent)
    {
        var plan = s.PlanId == _paypal.YearlyPlanId ? "yearly" : "monthly";
        var agreed = consent is null
            ? ""
            : $"<p>Before you paid, you agreed to this text (version "
              + $"{WebUtility.HtmlEncode(consent.Version)}):<br>"
              + $"{WebUtility.HtmlEncode(consent.Text)}</p>";
        return $"<p>Thank you. Your {plan} subscription to Foresight "
            + $"premium has started. PayPal's id for it is "
            + $"{WebUtility.HtmlEncode(s.Id)}.</p>"
            + agreed
            + "<p>You can cancel at any time on your subscription page. "
            + "Premium stays until the end of the period you paid for.</p>";
    }
}
