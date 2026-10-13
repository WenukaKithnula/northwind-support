using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// Signs a JWT for an agent. The token says who they are (id, name, email) and when it stops being
/// accepted, and nothing else: it is signed, not encrypted, so anyone holding it can read it, and it
/// must never carry anything secret such as the password hash.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtSettings> options, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(Agent agent)
    {
        var settings = options.Value;
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(settings.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, agent.FullName),
                new Claim(JwtRegisteredClaimNames.Email, agent.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
