namespace Foresight.Scheduler;

/// <summary>
/// One run at a time. A slow tick may still be posting when the next one
/// starts, and two runs on one schedule file could post an event twice.
/// </summary>
public static class RunLock
{
    /// <summary>The lock, or null when another run holds it.</summary>
    public static FileStream? TryTake(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            // FileShare.None: while this run keeps the file open, the
            // operating system refuses to open it a second time.
            return new FileStream(Path.Combine(folder, "scheduler.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
