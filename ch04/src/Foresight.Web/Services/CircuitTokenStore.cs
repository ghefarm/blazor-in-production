namespace Foresight.Web.Services;

/// <summary>
/// The login token of one visitor, kept in the memory of their circuit.
/// Scoped, so every circuit has its own. Nothing is stored in the browser:
/// a reload starts a new circuit, and the visitor signs in again.
/// </summary>
public sealed class CircuitTokenStore
{
    public string? Token { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Raised on sign-in and sign-out, for the layout.</summary>
    public event Action? Changed;

    public void SignIn(string token, string email)
    {
        Token = token;
        Email = email;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Token = null;
        Email = null;
        Changed?.Invoke();
    }
}
