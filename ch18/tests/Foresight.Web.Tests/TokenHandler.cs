using Foresight.Web.Services;

namespace Foresight.Web.Tests;

/// <summary>
/// The wrong way, kept for the tests of chapter 5: a message handler that
/// puts the visitor's token on every request. It compiles, it looks right,
/// and under Blazor Server it never sends a token.
/// </summary>
public sealed class TokenHandler : DelegatingHandler
{
    private readonly CircuitTokenStore _tokens;

    public TokenHandler(CircuitTokenStore tokens)
    {
        _tokens = tokens;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokens.Token is { } token)
            request.Headers.Authorization = new("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
