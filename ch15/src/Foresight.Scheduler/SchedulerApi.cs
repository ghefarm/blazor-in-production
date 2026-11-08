using System.Globalization;
using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Scheduler;

/// <summary>The few API calls the scheduler makes. Tests use a fake.
/// </summary>
public interface ISchedulerApi
{
    /// <summary>Every forecast of one feed on one day, premium included.
    /// </summary>
    Task<IReadOnlyList<ForecastDto>> ForecastsAsync(
        string feed, DateOnly day, CancellationToken ct = default);

    /// <summary>The kill switch of chapter 14.</summary>
    Task<bool> IsPostingOnAsync(CancellationToken ct = default);
}

/// <summary>The API over HTTP, with the scheduler's key on every call.
/// </summary>
public sealed partial class SchedulerApi : ISchedulerApi
{
    private readonly HttpClient _http;

    public SchedulerApi(string baseUrl, string key)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.Add(ApiHeaders.SchedulerKey, key);
    }

    public async Task<IReadOnlyList<ForecastDto>> ForecastsAsync(
        string feed, DateOnly day, CancellationToken ct = default)
    {
        var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await _http.GetFromJsonAsync<List<ForecastDto>>(
                $"api/scheduler/forecasts/{Uri.EscapeDataString(feed)}"
                + $"?day={date}", ct)
            ?? [];
    }

    public async Task<bool> IsPostingOnAsync(CancellationToken ct = default)
    {
        var state = await _http.GetFromJsonAsync<PostingSwitch>(
            "api/scheduler/posting", ct);
        return state?.On ?? true;
    }
}
