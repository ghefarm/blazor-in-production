namespace Foresight.Billing;

/// <summary>
/// The one answer to "may this account see premium now?", for the API and
/// for the account's subscription page alike.
/// </summary>
public sealed class PremiumAccess
{
    private static readonly TimeSpan AskPayPalEvery = TimeSpan.FromMinutes(10);

    private readonly ISubscriptionStore _store;
    private readonly IPayPalClient _paypal;
    private readonly TimeProvider _clock;

    public PremiumAccess(
        ISubscriptionStore store, IPayPalClient paypal, TimeProvider clock)
    {
        _store = store;
        _paypal = paypal;
        _clock = clock;
    }

    public async Task<bool> HasAsync(
        string? owner, CancellationToken ct = default) =>
        owner is not null
        && await CurrentAsync(owner, ct) is { } current
        && current.IsEntitled(_clock.GetUtcNow().UtcDateTime);

    /// <summary>The owner's subscription that matters most, or null.</summary>
    public async Task<SubscriptionRecord?> CurrentAsync(
        string owner, CancellationToken ct = default)
    {
        var current = await _store.FindCurrentAsync(owner, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (current is null || !current.LooksLapsed(now)
            || now - current.CheckedUtc < AskPayPalEvery)
            return current;

        // ACTIVE but past its grace usually means that the renewal event
        // never arrived. Before a paying customer loses access, ask PayPal,
        // at most every ten minutes. The ten minutes start with the attempt,
        // so a slow PayPal is not asked again on every request.
        await _store.TouchAsync(current.Id, ct);
        try
        {
            if (await _paypal.GetSubscriptionAsync(current.Id, ct) is { } fresh)
                await _store.UpsertAsync(current.Owner, fresh, ct);
        }
        catch (PayPalException)
        {
            return current; // what we know; PayPal is asked again later
        }

        return await _store.FindCurrentAsync(owner, ct);
    }
}
