using Microsoft.Extensions.Options;
using SupportDesk.Presentation.Security;

namespace SupportDesk.UnitTests.Presentation.Security;

public class JwtSettingsValidatorTests
{
    private static readonly JwtSettingsValidator Validator = new();

    private static JwtSettings Valid() => new()
    {
        Issuer = "SupportDesk.Api",
        Audience = "SupportDesk.Web",
        SigningKey = new string('k', 32),
        LifetimeMinutes = 60
    };

    private static ValidateOptionsResult Check(JwtSettings settings) => Validator.Validate(null, settings);

    [Fact]
    public void ValidSettings_Pass() =>
        Assert.True(Check(Valid()).Succeeded);

    [Fact]
    public void AKeyOfExactly32Bytes_Passes() =>
        Assert.True(Check(Valid() with { SigningKey = new string('k', 32) }).Succeeded);

    [Fact]
    public void AKeyShorterThan32Bytes_Fails()
    {
        var result = Check(Valid() with { SigningKey = new string('k', 31) });

        Assert.True(result.Failed);
        Assert.Contains("32", result.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingKey_FailsAndNamesTheSetting(string? key)
    {
        var result = Check(Valid() with { SigningKey = key! });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey", result.FailureMessage);
    }

    [Fact]
    public void TheKeyIsMeasuredInBytesNotCharacters()
    {
        // Sixteen two-byte characters is 32 bytes; sixteen ASCII characters is only 16.
        Assert.True(Check(Valid() with { SigningKey = new string('é', 16) }).Succeeded);
        Assert.True(Check(Valid() with { SigningKey = new string('k', 16) }).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ALifetimeOfZeroOrLess_Fails(int minutes) =>
        Assert.True(Check(Valid() with { LifetimeMinutes = minutes }).Failed);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AMissingIssuerOrAudience_Fails(string value)
    {
        Assert.True(Check(Valid() with { Issuer = value }).Failed);
        Assert.True(Check(Valid() with { Audience = value }).Failed);
    }

    [Fact]
    public void EveryProblemIsReportedAtOnce()
    {
        var result = Check(new JwtSettings { Issuer = "", Audience = "", SigningKey = "short", LifetimeMinutes = 0 });

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey", result.FailureMessage);
        Assert.Contains("Jwt:LifetimeMinutes", result.FailureMessage);
        Assert.Contains("Jwt:Issuer", result.FailureMessage);
    }
}
