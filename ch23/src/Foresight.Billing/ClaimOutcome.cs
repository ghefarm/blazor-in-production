namespace Foresight.Billing;

/// <summary>What became of a visitor's "this subscription is mine".</summary>
public enum ClaimOutcome
{
    /// <summary>Stored for the signed-in account.</summary>
    Recorded,

    /// <summary>The id cannot be a PayPal id. PayPal was not asked.</summary>
    NotAnId,

    /// <summary>PayPal does not know the id.</summary>
    NotFound,

    /// <summary>A real subscription, but not to one of our plans.</summary>
    WrongPlan,

    /// <summary>Tagged for another account, or not tagged at all.</summary>
    NotYours,
}
