namespace Foresight.Billing;

public sealed partial class SubscriptionManager
{
    /// <summary>
    /// Cancels the owner's subscriptions at PayPal because the owner asked.
    /// The period already paid for is kept, so premium stays until then.
    /// </summary>
    /// <returns>How many subscriptions PayPal was told to stop.</returns>
    /// <remarks>
    /// Throws when PayPal cannot be told, so that nobody reads "cancelled"
    /// for a subscription that is still billed. Safe to repeat: a second
    /// call finds nothing left to cancel.
    /// </remarks>
    public async Task<int> CancelAsync(
        string owner, CancellationToken ct = default)
    {
        var cancelled = await CancelBillableAsync(
            owner, "Cancelled by the subscriber", ct);

        foreach (var id in cancelled)
        {
            try
            {
                // Read it back, so the page says CANCELLED now and not
                // only when the webhook arrives.
                await SyncAsync(id, ct);
            }
            catch (PayPalException)
            {
                // The cancel itself went through. The webhook brings the
                // row up to date when PayPal reports it.
            }
        }
        return cancelled.Count;
    }

    /// <summary>
    /// The first step of deleting an account: nothing of the owner may stay
    /// billed. Throws when PayPal cannot be told.
    /// </summary>
    public Task CancelForDeletionAsync(
        string owner, CancellationToken ct = default) =>
        CancelBillableAsync(owner, AccountDeleted, ct);

    /// <summary>Removes the owner's rows. Call it after the cancel.</summary>
    public Task ForgetAsync(string owner, CancellationToken ct = default) =>
        _store.ForgetAsync(owner, ct);

    /// <summary>
    /// The last step of a deletion, once the account is gone: cancel first,
    /// then forget, for the rows a PayPal event stored in the meantime.
    /// Safe to repeat; with no rows it does nothing.
    /// </summary>
    /// <returns>The subscriptions PayPal could not be told to stop. Their
    /// rows are kept.</returns>
    public async Task<IReadOnlyList<(string Id, PayPalException Why)>>
        SweepAsync(string owner, CancellationToken ct = default)
    {
        var notTold = new List<(string, PayPalException)>();
        foreach (var row in await _store.FindAllAsync(owner, ct))
        {
            try
            {
                // The same rule and the same call as the first step.
                if (row.IsBillable)
                    await _paypal.CancelSubscriptionAsync(
                        row.Id, AccountDeleted, ct);
            }
            catch (PayPalException ex)
            {
                // The row may be our only trace of a subscription that
                // PayPal still bills, so it stays.
                notTold.Add((row.Id, ex));
                continue;
            }
            await _store.ForgetOneAsync(row.Id, ct);
        }
        return notTold;
    }

    private const string AccountDeleted = "Account deleted";

    // Asks PayPal to stop every subscription of the owner that could still
    // be billed. PayPal's client treats "already cancelled" as done.
    private async Task<List<string>> CancelBillableAsync(
        string owner, string reason, CancellationToken ct)
    {
        var billable = (await _store.FindAllAsync(owner, ct))
            .Where(r => r.IsBillable)
            .Select(r => r.Id)
            .ToList();
        foreach (var id in billable)
            await _paypal.CancelSubscriptionAsync(id, reason, ct);
        return billable;
    }
}
