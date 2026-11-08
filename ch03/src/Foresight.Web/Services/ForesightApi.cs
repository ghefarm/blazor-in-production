using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Web.Services;

/// <summary>The answer of one call: a value, or a sentence to show.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>The API as the pages see it.</summary>
public sealed class ForesightApi
{
    private readonly HttpClient _http;

    public ForesightApi(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("api");
    }

    public Task<ApiResult<List<ForecastDto>>> ForecastsAsync(
        string feed, CancellationToken ct = default) =>
        GetAsync<List<ForecastDto>>(
            $"api/forecasts/{Uri.EscapeDataString(feed)}", ct);

    private async Task<ApiResult<T>> GetAsync<T>(
        string path, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(path, ct);
        }
        catch (HttpRequestException)
        {
            return new(default, "The server cannot be reached just now. "
                + "Please try again in a few minutes.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return new(default, await ApiError.ReadAsync(response, ct));
            return new(await response.Content.ReadFromJsonAsync<T>(ct), null);
        }
    }
}
