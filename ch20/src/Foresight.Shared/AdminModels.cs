namespace Foresight.Shared;

/// <summary>Whether the scheduler may post: the kill switch.</summary>
public sealed record PostingSwitch(bool On);

/// <summary>One account as the admin page lists it. No hash, no key.
/// </summary>
public sealed record AdminAccount(
    string Email,
    bool EmailConfirmed,
    bool TwoFactor,
    DateTime? PremiumUntilUtc);

/// <summary>The account an admin tool acts on.</summary>
public sealed record AdminAccountRequest(string Email);

/// <summary>A sentence for the admin after a tool has run.</summary>
public sealed record AdminMessage(string Message);
