namespace Foresight.Billing;

/// <summary>A subscription's statuses, spelled as PayPal does.</summary>
public static class PayPalStatus
{
    // Created, but the buyer has not agreed yet. It unlocks nothing.
    public const string ApprovalPending = "APPROVAL_PENDING";

    // Agreed, the first payment not yet taken. Seconds, usually.
    public const string Approved = "APPROVED";

    public const string Active = "ACTIVE";

    // Payments failed more often than the plan allows; PayPal stopped.
    public const string Suspended = "SUSPENDED";

    public const string Cancelled = "CANCELLED";

    public const string Expired = "EXPIRED";
}
