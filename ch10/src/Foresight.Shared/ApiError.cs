using System.Net.Http.Json;
using System.Text.Json;

namespace Foresight.Shared;

/// <summary>Every error answer of the API: one sentence.</summary>
public sealed record ErrorResponse(string Error);

/// <summary>The sentence a page shows for an error answer of the API.</summary>
public static class ApiError
{
    /// <summary>
    /// The API's own sentence, or, when the answer has none, a sentence
    /// that names the kind of failure. Never a guess.
    /// </summary>
    public static async Task<string> ReadAsync(
        HttpResponseMessage response, CancellationToken ct = default)
    {
        try
        {
            var body = await response.Content
                .ReadFromJsonAsync<ErrorResponse>(ct);
            if (!string.IsNullOrWhiteSpace(body?.Error))
                return body.Error;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            // No JSON, or not ours: fall through to the status code.
        }

        var code = (int)response.StatusCode;
        return code >= 500
            ? $"The server could not do this just now (error {code}). "
              + "Please try again in a few minutes."
            : $"The request was refused (error {code}).";
    }
}
