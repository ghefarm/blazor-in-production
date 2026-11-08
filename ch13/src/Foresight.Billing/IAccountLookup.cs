namespace Foresight.Billing;

/// <summary>
/// Whether an account exists now. Billing knows an account only by its
/// email; the app answers from its users table.
/// </summary>
public interface IAccountLookup
{
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
}
