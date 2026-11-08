using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Foresight.Web.Tests;

/// <summary>
/// Stands in for the API at the end of the handler chain. It remembers the
/// Authorization header of every request and answers with a set status.
/// </summary>
public sealed class RecordingApi : HttpMessageHandler
{
    private readonly ConcurrentQueue<string?> _seen = new();

    /// <summary>The status of every answer: 200 unless a test says.</summary>
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <summary>The Authorization header of each request, in order.</summary>
    public IReadOnlyList<string?> Authorizations => _seen.ToList();

    public string? LastAuthorization => _seen.LastOrDefault();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _seen.Enqueue(request.Headers.Authorization?.ToString());
        return Task.FromResult(new HttpResponseMessage(Status)
        {
            Content = new StringContent("[]", Encoding.UTF8,
                "application/json"),
        });
    }
}
