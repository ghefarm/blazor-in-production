using System.Net;
using System.Net.Http.Json;
using Foresight.Shared;

namespace Foresight.Web.Services;

/// <summary>The answer of one call: a value, or a sentence to show.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}

/// <summary>
/// The API as the pages see it. Scoped, like the token store, so it always
/// holds the token of its own circuit and attaches it itself.
/// </summary>
public sealed partial class ForesightApi
{
    private readonly HttpClient _http;
    private readonly CircuitTokenStore _tokens;

    public ForesightApi(IHttpClientFactory factory, CircuitTokenStore tokens)
    {
        _http = factory.CreateClient("api");
        _tokens = tokens;
    }

    public async Task<ApiResult<LoginResponse>> SignInAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var result = await SendAsync<LoginResponse>(HttpMethod.Post,
            "api/auth/login", request, withToken: false, ct);
        if (result.Value?.Token is { } token)
            _tokens.SignIn(token, request.Email.Trim());
        return result;
    }

    public void SignOut() => _tokens.SignOut();

    public Task<ApiResult<bool>> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Post, "api/auth/register", request,
            withToken: false, ct);

    public Task<ApiResult<bool>> ConfirmEmailAsync(
        string email, string token, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Get, "api/auth/confirm-email"
            + $"?email={Uri.EscapeDataString(email)}"
            + $"&token={Uri.EscapeDataString(token)}",
            null, withToken: false, ct);

    public Task<ApiResult<bool>> ResendConfirmationAsync(
        string email, CancellationToken ct = default) =>
        SendAsync<bool>(HttpMethod.Post, "api/auth/resend-confirmation",
            new ResendConfirmationRequest(email), withToken: false, ct);

    public Task<ApiResult<List<ForecastDto>>> ForecastsAsync(
        string feed, CancellationToken ct = default) =>
        SendAsync<List<ForecastDto>>(HttpMethod.Get,
            $"api/forecasts/{Uri.EscapeDataString(feed)}", null, true, ct);

    public Task<ApiResult<TwoFactorStatus>> TwoFactorAsync(
        CancellationToken ct = default) =>
        SendAsync<TwoFactorStatus>(HttpMethod.Get, "api/auth/2fa", null,
            withToken: true, ct);

    public Task<ApiResult<TwoFactorSetup>> SetUpTwoFactorAsync(
        CancellationToken ct = default) =>
        SendAsync<TwoFactorSetup>(HttpMethod.Post, "api/auth/2fa/setup",
            null, withToken: true, ct);

    public Task<ApiResult<RecoveryCodes>> EnableTwoFactorAsync(
        string code, CancellationToken ct = default) =>
        SendAsync<RecoveryCodes>(HttpMethod.Post, "api/auth/2fa/enable",
            new TwoFactorCode(code), withToken: true, ct);

    public Task<ApiResult<TwoFactorStatus>> DisableTwoFactorAsync(
        string password, CancellationToken ct = default) =>
        SendAsync<TwoFactorStatus>(HttpMethod.Post, "api/auth/2fa/disable",
            new PasswordRequest(password), withToken: true, ct);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method,
        string path, object? body, bool withToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());

        // The token is attached here, by a service of the circuit itself.
        // Chapter 5 tells why a message handler cannot do this.
        if (withToken && _tokens.Token is { } token)
            request.Headers.Authorization = new("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            return new(default, "The server cannot be reached just now. "
                + "Please try again in a few minutes.");
        }

        using (response)
        {
            // The API no longer takes the token: it expired, or its
            // account is gone. Sign out here as well.
            if (withToken && response.StatusCode == HttpStatusCode.Unauthorized)
                _tokens.SignOut();

            if (!response.IsSuccessStatusCode)
                return new(default, await ApiError.ReadAsync(response, ct));
            if (typeof(T) == typeof(bool))
                return new((T)(object)true, null);
            return new(await response.Content.ReadFromJsonAsync<T>(ct), null);
        }
    }
}
