namespace Foresight.Api.TrackRecord;

/// <summary>The "TrackRecord" section of the settings.</summary>
public sealed class TrackRecordOptions
{
    public const string SectionName = "TrackRecord";

    // Where the scheduler writes its schedule-YYYY-MM-DD.json files
    // (chapter 15). Empty means the "schedule" folder beside the database.
    // The API only reads there.
    public string ScheduleFolder { get; set; } = "";
}
