namespace Foresight.Shared;

/// <summary>
/// Asks for the confirmation link again. Only the email: a password would
/// prove nothing here, because the link goes to that address, and only
/// whoever can read its inbox can use it.
/// </summary>
public sealed record ResendConfirmationRequest(string Email);
