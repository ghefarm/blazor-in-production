using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Foresight.Api.Accounts;

/// <summary>Signs the login token the website keeps for a visitor.</summary>
public sealed class TokenIssuer
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public TokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string IssueFor(string email)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.Key));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, email),
            ]),
            Expires = _clock.GetUtcNow().UtcDateTime.AddDays(_options.Days),
            SigningCredentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256),
        });
    }
}
