using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Foresight.Billing;

/// <summary>
/// The PayPal REST calls the billing code needs. What a page or a webhook
/// says is checked against these answers.
/// </summary>
public sealed class PayPalClient : IPayPalClient
{
    public const string HttpClientName = "paypal";

    private readonly IHttpClientFactory _httpFactory;
    private readonly PayPalOptions _options;

    // An access token lasts hours, so one is kept until shortly before it
    // runs out. The gate stops two callers from each fetching their own.
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _token;
    private DateTime _tokenExpiresUtc;

    public PayPalClient(
        IHttpClientFactory httpFactory, IOptions<PayPalOptions> options)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
    }

    public async Task<PayPalSubscription?> GetSubscriptionAsync(
        string subscriptionId, CancellationToken ct = default)
    {
        var path = "v1/billing/subscriptions/"
            + Uri.EscapeDataString(subscriptionId);
        if (await GetJsonOrNullAsync(path, ct) is not { } root) return null;

        // PayPal sends ISO 8601 with a Z, which is read as UTC.
        DateTime? nextBilling = null;
        if (root.TryGetProperty("billing_info", out var billing)
            && billing.ValueKind == JsonValueKind.Object
            && billing.TryGetProperty("next_billing_time", out var next)
            && next.ValueKind == JsonValueKind.String
            && next.TryGetDateTime(out var parsed))
            nextBilling = parsed;

        return new PayPalSubscription(
            Id: StringOrNull(root, "id") ?? subscriptionId,
            Status: StringOrNull(root, "status") ?? "",
            PlanId: StringOrNull(root, "plan_id") ?? "",
            CustomId: StringOrNull(root, "custom_id"),
            NextBillingUtc: nextBilling);
    }

    /// <summary>
    /// The subscription a payment (a "sale") paid for, or null when PayPal
    /// does not know the sale or it was a one-off payment. A refund event
    /// may name only the sale.
    /// </summary>
    public async Task<string?> GetSaleSubscriptionIdAsync(
        string saleId, CancellationToken ct = default)
    {
        var path = "v1/payments/sale/" + Uri.EscapeDataString(saleId);
        return await GetJsonOrNullAsync(path, ct) is { } sale
            ? StringOrNull(sale, "billing_agreement_id")
            : null;
    }

    /// <summary>
    /// Stops the subscription so PayPal does not bill it again. One that is
    /// unknown or no longer active counts as done: that is the outcome we
    /// asked for.
    /// </summary>
    public async Task CancelSubscriptionAsync(
        string subscriptionId, string reason, CancellationToken ct = default)
    {
        var path = "v1/billing/subscriptions/"
            + Uri.EscapeDataString(subscriptionId) + "/cancel";
        using var response = await SendAsync(HttpMethod.Post, path,
            JsonSerializer.Serialize(new { reason }), ct);

        if (response.IsSuccessStatusCode
            || response.StatusCode == HttpStatusCode.NotFound) return;

        // PayPal's answer when the subscription is already cancelled.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity
            && (await response.Content.ReadAsStringAsync(ct))
                .Contains("SUBSCRIPTION_STATUS_INVALID")) return;

        await EnsureSuccessAsync(response, ct);
    }

    /// <summary>
    /// Asks PayPal whether a webhook call is really its own. The event goes
    /// back byte for byte: PayPal checks a checksum of it, and writing it out
    /// again could change a byte.
    /// </summary>
    public async Task<bool> VerifyWebhookAsync(
        IHeaderDictionary headers,
        string rawBody,
        CancellationToken ct = default)
    {
        if (_options.WebhookId.Length == 0) return false;

        string? Header(string name) => headers[name].FirstOrDefault();
        var algo = Header("PAYPAL-AUTH-ALGO");
        var certUrl = Header("PAYPAL-CERT-URL");
        var id = Header("PAYPAL-TRANSMISSION-ID");
        var signature = Header("PAYPAL-TRANSMISSION-SIG");
        var time = Header("PAYPAL-TRANSMISSION-TIME");
        if (algo is null || certUrl is null || id is null
            || signature is null || time is null) return false;

        // Anyone can send these headers. PayPal serves its certificate from
        // its own domain, so any other address is not worth a PayPal call.
        if (!Uri.TryCreate(certUrl, UriKind.Absolute, out var cert)
            || cert.Scheme != Uri.UriSchemeHttps
            || !cert.Host.EndsWith(
                ".paypal.com", StringComparison.OrdinalIgnoreCase))
            return false;

        using var json = new MemoryStream();
        using (var writer = new Utf8JsonWriter(json))
        {
            writer.WriteStartObject();
            writer.WriteString("auth_algo", algo);
            writer.WriteString("cert_url", certUrl);
            writer.WriteString("transmission_id", id);
            writer.WriteString("transmission_sig", signature);
            writer.WriteString("transmission_time", time);
            writer.WriteString("webhook_id", _options.WebhookId);
            writer.WritePropertyName("webhook_event");
            writer.WriteRawValue(rawBody); // exactly as it arrived
            writer.WriteEndObject();
        }

        using var response = await SendAsync(HttpMethod.Post,
            "v1/notifications/verify-webhook-signature",
            Encoding.UTF8.GetString(json.ToArray()), ct);
        await EnsureSuccessAsync(response, ct);

        var verdict =
            await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return verdict.ValueKind == JsonValueKind.Object
            && StringOrNull(verdict, "verification_status") == "SUCCESS";
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? jsonBody, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(ct);

            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            if (jsonBody is not null)
                request.Content = new StringContent(
                    jsonBody, Encoding.UTF8, "application/json");

            var response = await SendRawAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized
                || attempt > 0) return response;

            // The token was revoked or ran out early: fetch a new one, once.
            response.Dispose();
            _token = null;
        }
    }

    // Callers handle one kind of failure: PayPal unreachable or too slow is
    // reported like any other answer we cannot use.
    private async Task<HttpResponseMessage> SendRawAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _httpFactory.CreateClient(HttpClientName)
                .SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new PayPalException("PayPal could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PayPalException("PayPal did not answer in time.", ex);
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        await _tokenGate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpiresUtc)
                return _token;

            using var request =
                new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
                {
                    Content = new FormUrlEncodedContent(
                        [new("grant_type", "client_credentials")]),
                };
            var credentials = Encoding.UTF8.GetBytes(
                $"{_options.ClientId}:{_options.ClientSecret}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(credentials));

            using var response = await SendRawAsync(request, ct);
            await EnsureSuccessAsync(response, ct);

            var root =
                await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (root.ValueKind != JsonValueKind.Object)
                throw new PayPalException("PayPal sent no access token.");
            var expiresIn = root.TryGetProperty("expires_in", out var e)
                && e.TryGetInt32(out var seconds) ? seconds : 300;

            _token = StringOrNull(root, "access_token")
                ?? throw new PayPalException("PayPal sent no access token.");
            _tokenExpiresUtc =
                DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 60, 30));
            return _token;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    // GET a path and read the JSON answer; null when PayPal does not know
    // what the path names.
    private async Task<JsonElement?> GetJsonOrNullAsync(
        string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);

        var root =
            await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return root.ValueKind == JsonValueKind.Object ? root : null;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        // PayPal's error body names the problem and carries a debug_id that
        // its support can look up.
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 400) body = body[..400];
        throw new PayPalException(
            $"PayPal answered {(int)response.StatusCode}: {body}");
    }

    private static string? StringOrNull(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
