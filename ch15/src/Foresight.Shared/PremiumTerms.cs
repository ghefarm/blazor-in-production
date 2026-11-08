namespace Foresight.Shared;

/// <summary>
/// The words a buyer agrees to before PayPal opens. The page shows them,
/// the server stores them, and the contract mail repeats them, so the three
/// can never say different things.
/// </summary>
public static class PremiumTerms
{
    // Changes with every change of ConsentText. Each consent is stored with
    // its version and its text, so it can always be shown what was agreed.
    public const string ConsentVersion = "2026-11-03";

    public const string ConsentText =
        "I agree that Foresight unlocks premium at once, before the 14-day "
        + "withdrawal period ends. I know that I lose my right of withdrawal "
        + "when premium is unlocked.";
}

/// <summary>The consent box was ticked on a page with this version.</summary>
public sealed record ConsentRequest(string TextVersion);
