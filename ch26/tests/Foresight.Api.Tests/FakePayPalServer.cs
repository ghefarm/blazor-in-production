using System.Net;
using System.Text;
using System.Text.Json;

namespace Foresight.Api.Tests;

/// <summary>
/// PayPal's REST API as the HTTP client sees it: the few paths our client
/// calls, answered in PayPal's shapes, and every request written down. It
/// sits below the real <c>PayPalClient</c>, so the client's own HTTP code
/// runs in the test as it does in production.
/// </summary>
internal sealed class FakePayPalServer
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _subscriptions = new();
    private readonly List<string> _requests = new();

    /// <summary>"METHOD path" of every call, in order.</summary>
    public IReadOnlyList<string> Requests
    {
        get { lock (_gate) return _requests.ToArray(); }
    }

    /// <summary>A subscription as PayPal would return it; its new id.</summary>
    public string AddSubscription(string status, string planId, string owner)
    {
        var id = $"I-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var json = JsonSerializer.Serialize(new
        {
            id,
            status,
            plan_id = planId,
            custom_id = owner,
            billing_info = new { next_billing_time = "2026-12-04T10:00:00Z" },
        });
        lock (_gate) _subscriptions[id] = json;
        return id;
    }

    /// <summary>A new handler for the client factory, on this server.</summary>
    public HttpMessageHandler Handler() => new FakeHandler(this);

    private (HttpStatusCode, string) Answer(HttpMethod method, string path)
    {
        lock (_gate) _requests.Add($"{method} {path}");
        string[] parts = path.Split('/');

        return (method.Method, parts) switch
        {
            ("POST", [_, "oauth2", "token"]) => (HttpStatusCode.OK,
                """{"access_token":"A21-FAKE","expires_in":32400}"""),
            ("POST", [_, "notifications", "verify-webhook-signature"]) =>
                (HttpStatusCode.OK, """{"verification_status":"SUCCESS"}"""),
            ("GET", [_, "billing", "subscriptions", var id]) =>
                Subscription(id),
            ("POST", [_, "billing", "subscriptions", _, "cancel"]) =>
                (HttpStatusCode.NoContent, ""),
            _ => (HttpStatusCode.NotFound,
                """{"name":"RESOURCE_NOT_FOUND"}"""),
        };
    }

    private (HttpStatusCode, string) Subscription(string id)
    {
        lock (_gate)
            return _subscriptions.TryGetValue(id, out var json)
                ? (HttpStatusCode.OK, json)
                : (HttpStatusCode.NotFound,
                    """{"name":"RESOURCE_NOT_FOUND"}""");
    }

    /// <summary>The HTTP side: a request in, the server's answer out.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly FakePayPalServer _server;

        public FakeHandler(FakePayPalServer server)
        {
            _server = server;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            var (status, body) = _server.Answer(request.Method, path);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
