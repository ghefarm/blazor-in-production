using Foresight.Api.Channels;
using Foresight.Api.Data;
using Foresight.Billing;
using Microsoft.AspNetCore.Identity;

namespace Foresight.Api.Accounts;

/// <summary>
/// Deletes an account in the one order that never leaves a customer billed
/// or data behind. Used by the account's owner and by the admin.
/// </summary>
public sealed class AccountDeletion
{
    public enum Result { Deleted, PayPalFailed, Failed }

    private readonly UserManager<AppUser> _users;
    private readonly SubscriptionManager _subscriptions;
    private readonly ContractStore _contracts;
    private readonly ChannelMembership _channel;
    private readonly ILogger<AccountDeletion> _logger;

    public AccountDeletion(
        UserManager<AppUser> users,
        SubscriptionManager subscriptions,
        ContractStore contracts,
        ChannelMembership channel,
        ILogger<AccountDeletion> logger)
    {
        _users = users;
        _subscriptions = subscriptions;
        _contracts = contracts;
        _channel = channel;
        _logger = logger;
    }

    /// <param name="by">Who asked, for the log: its owner or the admin.</param>
    public async Task<Result> DeleteAsync(
        AppUser user, string by, CancellationToken ct = default)
    {
        var email = user.Email!;

        // 1. PayPal first. If PayPal cannot be told, nothing is deleted:
        // an account that still exists can try again, but nobody can sign
        // in to a deleted account to stop its payments.
        try
        {
            await _subscriptions.CancelForDeletionAsync(email, ct);
        }
        catch (PayPalException ex)
        {
            _logger.LogError(ex, "Not deleted, PayPal not told: {Email}",
                email);
            return Result.PayPalFailed;
        }

        // 2. Our own data, the account last. Every step is safe to repeat,
        // so after a failure the owner can simply delete again.
        var step = "forget the subscriptions";
        try
        {
            await _subscriptions.ForgetAsync(email, ct);
            step = "forget the contract records";
            await _contracts.ForgetAsync(email, ct);
            step = "leave the premium channel";
            await _channel.ForgetAsync(email, ct);
            step = "delete the account";
            var deleted = await _users.DeleteAsync(user);
            if (!deleted.Succeeded)
                throw new InvalidOperationException(string.Join("; ",
                    deleted.Errors.Select(e => e.Description)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Deleting {Email} stopped at: {Step}",
                email, step);
            return Result.Failed;
        }

        // 3. Cancel first, then forget, once more. While the steps above
        // ran, the account existed, so a PayPal event could store a row:
        // even one for a subscription approved seconds before, which step 1
        // never saw. Now the check of chapter 7 keeps every new row out.
        step = "cancel and forget late subscriptions";
        try
        {
            foreach (var (id, why) in
                await _subscriptions.SweepAsync(email, ct))
                _logger.LogError(why, "Deleted, but {Step}: PayPal was not "
                    + "told to stop {Id}; its row and its contract records "
                    + "are kept: {Email}", step, id, email);
            // Only what was cancelled: a kept row keeps its records.
            await _contracts.ForgetAsync(email, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Deleted, but {Step} stopped: {Email}",
                step, email);
        }
        // The account is gone, so the answer is "deleted" either way.

        _logger.LogInformation("Account deleted by {By}: {Email}", by, email);
        return Result.Deleted;
    }
}
