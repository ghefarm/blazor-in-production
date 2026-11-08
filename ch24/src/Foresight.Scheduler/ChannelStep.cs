using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Scheduler;

/// <summary>The API call that brings the premium channel in line with the
/// subscriptions. Tests use a fake.</summary>
public interface IPremiumChannelApi
{
    Task<ChannelSyncReport> SyncPremiumChannelAsync(
        CancellationToken ct = default);
}

public sealed partial class SchedulerApi : IPremiumChannelApi
{
    // The API does the work: it holds the subscriptions and the bot's
    // token. The scheduler only says when, with its key.
    public async Task<ChannelSyncReport> SyncPremiumChannelAsync(
        CancellationToken ct = default)
    {
        using var answer = await _http.PostAsync(
            "api/premium-channel/sync", null, ct);
        if (!answer.IsSuccessStatusCode)
            throw new HttpRequestException(
                await ApiError.ReadAsync(answer, ct));
        return (await answer.Content
            .ReadFromJsonAsync<ChannelSyncReport>(ct))!;
    }
}

/// <summary>
/// The first step of every real tick. A failure is reported and never
/// thrown: a channel that could not be synced must not stop the posts.
/// </summary>
public sealed class ChannelStep
{
    private readonly IPremiumChannelApi _api;
    private readonly TextWriter _log;

    public ChannelStep(IPremiumChannelApi api, TextWriter log)
    {
        _api = api;
        _log = log;
    }

    /// <returns>Null when the sync worked, else what went wrong. The next
    /// tick tries again either way.</returns>
    public async Task<string?> RunAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await _api.SyncPremiumChannelAsync(ct);
            if (r.Approved + r.Declined + r.Removed > 0)
                _log.WriteLine($"Premium channel: {r.Approved} let in, "
                    + $"{r.Declined} turned away, {r.Removed} removed.");
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var problem = $"The premium channel was not synced: {ex.Message}";
            _log.WriteLine(problem);
            return problem;
        }
    }
}
