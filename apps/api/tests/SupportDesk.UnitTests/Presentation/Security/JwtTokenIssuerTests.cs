using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Presentation.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation.Security;

public class JwtTokenIssuerTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static JwtSettings Settings(string key = "test-signing-key-that-is-at-least-32-bytes") => new()
    {
        Issuer = "SupportDesk.Api",
        Audience = "SupportDesk.Web",
        SigningKey = key,
        LifetimeMinutes = 60
    };

    private static Agent Alex()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now).WithId(7);
        agent.SetPasswordHash("a-salted-hash");

        return agent;
    }

    private static JwtTokenIssuer Issuer(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? Settings()), new FixedClock());

    private static async Task<TokenValidationResult> Validate(string token, JwtSettings settings, DateTime? validatedAt = null) =>
        await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
                (validatedAt ?? Now) >= notBefore && (validatedAt ?? Now) < expires
        });

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredLifetime()
    {
        var issued = Issuer().Issue(Alex());

        Assert.Equal(Now.AddMinutes(60), issued.ExpiresAtUtc);
    }

    [Fact]
    public void Issue_FollowsADifferentConfiguredLifetime()
    {
        var issued = Issuer(Settings() with { LifetimeMinutes = 15 }).Issue(Alex());

        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAtUtc);
    }

    [Fact]
    public async Task Issue_ProducesATokenTheSigningKeyValidates()
    {
        var issued = Issuer().Issue(Alex());

        var result = await Validate(issued.Token, Settings());

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task Issue_ProducesATokenAnotherKeyRejects()
    {
        var issued = Issuer().Issue(Alex());

        var result = await Validate(issued.Token, Settings("a-completely-different-signing-key-32-bytes"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Issue_ProducesATokenThatStopsBeingValidAtItsExpiry()
    {
        var issued = Issuer().Issue(Alex());

        Assert.True((await Validate(issued.Token, Settings(), Now.AddMinutes(59))).IsValid);
        Assert.False((await Validate(issued.Token, Settings(), Now.AddMinutes(60))).IsValid);
    }

    [Fact]
    public async Task Issue_CarriesTheAgentsIdNameAndEmail()
    {
        var issued = Issuer().Issue(Alex());

        var result = await Validate(issued.Token, Settings());
        var claims = result.Claims;

        Assert.Equal("7", claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("Alex Turner", claims[JwtRegisteredClaimNames.Name]);
        Assert.Equal("alex.turner@northwind-support.example", claims[JwtRegisteredClaimNames.Email]);
    }

    [Fact]
    public void Issue_NeverPutsThePasswordHashInTheToken()
    {
        var issued = Issuer().Issue(Alex());

        // A token is only signed, not encrypted: anyone holding it can read it.
        var payload = new JsonWebToken(issued.Token).EncodedPayload;
        var decoded = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(payload));

        Assert.DoesNotContain("a-salted-hash", decoded);
        Assert.DoesNotContain("hash", decoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issue_NamesTheIssuerAndAudienceFromConfiguration()
    {
        var settings = Settings() with { Issuer = "Somebody.Else", Audience = "Another.Audience" };

        var result = await Validate(Issuer(settings).Issue(Alex()).Token, settings);

        Assert.True(result.IsValid);
        Assert.Equal("Somebody.Else", result.Claims[JwtRegisteredClaimNames.Iss]);
    }
}
