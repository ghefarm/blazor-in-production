namespace Foresight.Shared;

/// <summary>
/// Headers the website and the scheduler send and the API reads. In one
/// place, because a name that differs between two projects still compiles:
/// the API would simply stop knowing its callers.
/// </summary>
public static class ApiHeaders
{
    /// <summary>Proves a call comes from our own scheduler.</summary>
    public const string SchedulerKey = "X-Scheduler-Key";
}
