namespace Foresight.Api.Data;

/// <summary>
/// A posted forecast that said yes, and what became of it. Derived: every
/// refresh builds the table again from the scheduler's files.
/// </summary>
public sealed class RecordRow
{
    public int Id { get; set; }

    public string EventId { get; set; } = "";

    public string Feed { get; set; } = "";

    public string Title { get; set; } = "";

    public DateTime StartsUtc { get; set; }

    public string Outcome { get; set; } = "";

    // As it was posted, never worked out again.
    public double Probability { get; set; }

    public bool IsPremium { get; set; }

    // Null until its outcome is found.
    public bool? Happened { get; set; }
}

/// <summary>
/// An event as the outcome source reports it, forecast or not. Derived,
/// like RecordRow; the baseline is counted from these.
/// </summary>
public sealed class OutcomeRow
{
    public int Id { get; set; }

    public string Feed { get; set; } = "";

    // The source's own spelling.
    public string Name { get; set; } = "";

    // The source's own clock: the local time of the place.
    public DateTime StartsLocal { get; set; }

    public bool Happened { get; set; }
}
