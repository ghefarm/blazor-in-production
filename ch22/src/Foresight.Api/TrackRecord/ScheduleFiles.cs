using System.Text.Json;
using Foresight.Shared;
using Microsoft.Extensions.Options;

namespace Foresight.Api.TrackRecord;

/// <summary>
/// Reads what the scheduler posted from its schedule files (chapter 15).
/// The file is the contract between the two programs: the API does not
/// depend on the scheduler's code, only on the fields read here.
/// </summary>
public sealed class ScheduleFiles : IPostedForecasts
{
    private static readonly JsonSerializerOptions Json =
        new() { PropertyNameCaseInsensitive = true };

    private readonly TrackRecordOptions _options;
    private readonly ILogger<ScheduleFiles> _logger;

    public ScheduleFiles(
        IOptions<TrackRecordOptions> options, ILogger<ScheduleFiles> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ForecastDto>> ReadAsync(
        CancellationToken ct = default)
    {
        var posted = new List<ForecastDto>();
        if (!Directory.Exists(_options.ScheduleFolder))
        {
            _logger.LogWarning("No schedule folder at {Folder}",
                _options.ScheduleFolder);
            return posted;
        }

        var files = Directory
            .EnumerateFiles(_options.ScheduleFolder, "schedule-*.json")
            .Order();
        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var day = await JsonSerializer.DeserializeAsync<ScheduleFile>(
                    stream, Json, ct);
                posted.AddRange((day?.Events ?? [])
                    .Where(e => e.Forecast is not null && string.Equals(
                        e.Status, "posted", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Forecast!));
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // One broken file must not hide the others.
                _logger.LogWarning(ex, "Skipped the schedule file {File}",
                    file);
            }
        }
        return posted;
    }

    // The fields of a schedule file this reads; the rest is the scheduler's.
    // Forecast is exactly what went out, as the API had sent it.
    private sealed class ScheduleFile
    {
        public List<Entry> Events { get; set; } = [];
    }

    private sealed class Entry
    {
        public string Status { get; set; } = "";

        public ForecastDto? Forecast { get; set; }
    }
}
