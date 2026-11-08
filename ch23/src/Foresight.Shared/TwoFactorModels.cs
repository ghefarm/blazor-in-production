namespace Foresight.Shared;

/// <summary>Two-factor sign-in of the signed-in account.</summary>
/// <param name="Available">False for the admin, who is not offered it.</param>
public sealed record TwoFactorStatus(
    bool Available, bool Enabled, int RecoveryCodesLeft);

/// <summary>The key to type into an authenticator app, in groups.</summary>
public sealed record TwoFactorSetup(string Key);

/// <summary>A code from the authenticator app.</summary>
public sealed record TwoFactorCode(string Code);

/// <summary>Ten codes for a lost phone. Each one works once.</summary>
public sealed record RecoveryCodes(IReadOnlyList<string> Codes);

/// <summary>The password, asked again before a change.</summary>
public sealed record PasswordRequest(string Password);
