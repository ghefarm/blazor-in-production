using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Foresight.Billing;

/// <summary>
/// Keeps the stored subscriptions correct when PayPal calls the anonymous
/// subscription webhook.
/// </summary>
/// <remarks>
/// Anyone can POST to the webhook, so the body is only a hint about which
/// subscription to look at. Everything that is stored is read from PayPal.
/// </remarks>
public sealed partial class SubscriptionManager
{
    // Real PayPal events are a few kilobytes. The endpoint must cap the
    // request before it reads it; this second cap stops a huge string that
    // got through anyway from being parsed.
    private const int MaxBodyLength = 64 * 1024;

    private const string SubscriptionEventPrefix = "BILLING.SUBSCRIPTION.";
    private const string SaleCompleted = "PAYMENT.SALE.COMPLETED";
    private const string SaleRefunded = "PAYMENT.SALE.REFUNDED";
    private const string SaleReversed = "PAYMENT.SALE.REVERSED";

    private const string CancelReason =
        "The payment for this subscription was refunded or reversed.";

    private readonly IPayPalClient _paypal;
    private readonly ISubscriptionStore _store;
    private readonly HashSet<string> _premiumPlanIds;
    private readonly ISubscriptionListener? _listener;
    private readonly IAccountLookup? _accounts;

    /// <summary>Creates a manager that looks after the given plans.</summary>
    /// <param name="listener">Told after every stored subscription.</param>
    /// <param name="accounts">
    /// Asked whether an owner still has an account. Without it, every owner
    /// that PayPal names counts as one.
    /// </param>
    public SubscriptionManager(
        IPayPalClient paypal,
        ISubscriptionStore store,
        IReadOnlyCollection<string> premiumPlanIds,
        ISubscriptionListener? listener = null,
        IAccountLookup? accounts = null)
    {
        ArgumentNullException.ThrowIfNull(paypal);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(premiumPlanIds);

        _paypal = paypal;
        _store = store;
        _listener = listener;
        _accounts = accounts;
        // A copy, so a caller who changes its collection later cannot
        // change which plans count as ours.
        _premiumPlanIds = new HashSet<string>(
            premiumPlanIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
    }

    /// <summary>Handles one call to the PayPal webhook.</summary>
    /// <param name="headers">The request headers, for the signature.</param>
    /// <param name="body">The raw request body, exactly as received.</param>
    /// <param name="ct">Cancels the PayPal and store calls.</param>
    /// <returns>
    /// False when the call is refused: the body is not a JSON object or
    /// PayPal does not vouch for it. True when it is acknowledged, which
    /// includes events that need no action.
    /// </returns>
    /// <remarks>
    /// Errors from PayPal or the store are not caught. The endpoint then
    /// answers with an error and PayPal sends the event again later.
    /// </remarks>
    public async Task<bool> HandleWebhookAsync(
        IHeaderDictionary headers,
        string body,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        if (body.Length > MaxBodyLength)
            return false;

        WebhookTarget? target;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            target = ReadTarget(document.RootElement);
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            // ArgumentException is what a lone UTF-16 surrogate causes: the
            // text cannot become UTF-8, so it is not JSON either.
            return false;
        }

        // Nothing to act on. Acknowledge it so PayPal stops sending it, and
        // spend no PayPal call on a body that anyone could have written.
        if (target is null)
            return true;

        if (!await _paypal.VerifyWebhookAsync(headers, body, ct))
            return false;

        if (target.IsRefund)
            await EndAccessForSaleAsync(target.Id, ct);
        else
            await SyncAsync(target.Id, ct);
        return true;
    }

    // Finds the one id the event points at, or null when there is nothing
    // to act on. The id is checked here, so a bad one never leaves this
    // class, not even for the signature check.
    private static WebhookTarget? ReadTarget(JsonElement root)
    {
        var type = ReadString(root, "event_type");
        if (type is null)
            return null;

        var resource = root.TryGetProperty("resource", out var found)
            ? found
            : default;

        string? id;
        var isRefund = false;
        if (type.StartsWith(SubscriptionEventPrefix, StringComparison.Ordinal))
        {
            id = ReadString(resource, "id");
        }
        else if (type == SaleCompleted)
        {
            id = ReadString(resource, "billing_agreement_id");
        }
        else if (type is SaleRefunded or SaleReversed)
        {
            // A refund names the sale in sale_id; a reversal is the sale.
            // A sale_id that is there but malformed is not replaced by id,
            // because a refund's own id is not a sale id.
            id = ReadString(resource, "sale_id")
                ?? ReadString(resource, "id");
            isRefund = true;
        }
        else
        {
            return null;
        }

        return id is not null && IsValidId(id)
            ? new WebhookTarget(id, isRefund)
            : null;
    }

    // A field that is missing or is not a JSON string counts as missing,
    // so a number, an object or null can never become an id.
    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return value.GetString();
        }
        catch (InvalidOperationException)
        {
            // An escaped half of a UTF-16 pair, such as "\ud800", is valid
            // JSON but cannot become a .NET string. A string we cannot read
            // counts as missing, like a field of the wrong type.
            return null;
        }
    }

    private static bool IsValidId(string id) => IdPattern().IsMatch(id);

    // \z and not $: in .NET, $ also matches before a final "\n", which
    // would let "I-ABC\n" through.
    [GeneratedRegex(@"^[A-Za-z0-9-]{3,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    // Stores what PayPal says now, whatever the event claimed, so a late or
    // repeated event writes the current state and never an old one.
    private async Task SyncAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        var ours = await GetOursAsync(subscriptionId, ct);
        if (ours is not null)
            await RecordAsync(ours.Owner, ours.Subscription, ct);
    }

    // The one way a subscription is stored, for the webhook and the claim.
    private async Task RecordAsync(
        string owner,
        PayPalSubscription subscription,
        CancellationToken ct = default)
    {
        // A revoked row is final. The store keeps the flag on upsert too,
        // but access must not come back if a store ever forgets that.
        var row = await _store.FindAsync(subscription.Id, ct);
        if (row is { Revoked: true })
            return;

        await _store.UpsertAsync(owner, subscription, ct);
        if (_listener is not null)
            await _listener.RecordedAsync(owner, subscription, ct);
    }

    private async Task EndAccessForSaleAsync(
        string saleId, CancellationToken ct = default)
    {
        // The event does not say which subscription the sale paid for in a
        // way we can trust, so PayPal is asked.
        var subscriptionId =
            await _paypal.GetSaleSubscriptionIdAsync(saleId, ct);
        if (subscriptionId is null || !IsValidId(subscriptionId))
            return;

        // End access before anything else that can fail. It does nothing
        // when we hold no row for this subscription.
        await _store.RevokeAsync(subscriptionId, ct);

        var ours = await GetOursAsync(subscriptionId, ct);
        if (ours is null)
            return;

        // The refund can arrive before the event that creates the row.
        // Create the row and revoke it, so the late event finds it revoked.
        if (await _store.FindAsync(subscriptionId, ct) is null)
        {
            await _store.UpsertAsync(ours.Owner, ours.Subscription, ct);
            await _store.RevokeAsync(subscriptionId, ct);
        }

        // A cancelled or expired subscription bills no more, so there is
        // nothing left to stop.
        if (IsFinished(ours.Subscription.Status))
            return;

        // Not caught: if the cancel fails, the endpoint must answer with an
        // error so that PayPal sends the event again and we retry.
        await _paypal.CancelSubscriptionAsync(subscriptionId, CancelReason, ct);
    }

    // Reads the subscription from PayPal and returns it only when it is on
    // one of our plans and names the account that owns it.
    private async Task<OwnedSubscription?> GetOursAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        var subscription =
            await _paypal.GetSubscriptionAsync(subscriptionId, ct);

        // An answer about another subscription would be a fault in the
        // client; it must never be stored under this id.
        if (subscription is null || subscription.Id != subscriptionId)
            return null;

        if (!_premiumPlanIds.Contains(subscription.PlanId))
            return null;

        // The custom_id names the owning account. Without it there is
        // nobody to give access to.
        var owner = subscription.CustomId;
        if (string.IsNullOrWhiteSpace(owner))
            return null;

        // A deleted account is nobody, too. PayPal still reports on its
        // subscription: the cancellation that the deletion itself sent, or
        // a refund given later. Storing what PayPal says would bring the
        // deleted account's email back into the database, and the deletion
        // never runs again.
        if (_accounts is not null && !await _accounts.ExistsAsync(owner, ct))
            return null;

        return new OwnedSubscription(subscription, owner);
    }

    private static bool IsFinished(string status) =>
        string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "EXPIRED", StringComparison.OrdinalIgnoreCase);

    // The one id an event points at, and whether it is a refunded sale.
    private sealed record WebhookTarget(string Id, bool IsRefund);

    // A subscription on one of our plans, with the account that owns it.
    private sealed record OwnedSubscription(
        PayPalSubscription Subscription, string Owner);
}
