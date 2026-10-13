using System.Text;
using Microsoft.Extensions.Options;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// The <c>Jwt</c> configuration section. The signing key is never in code: it comes from
/// configuration, and outside development from the environment (<c>Jwt__SigningKey</c>).
/// </summary>
public sealed record JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>The shortest key HMAC-SHA256 should be given: 32 bytes, as long as its output.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Signs and checks every token. Whoever has it can make a token for anyone.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>How long a token is accepted after it is issued.</summary>
    public int LifetimeMinutes { get; init; }
}

/// <summary>
/// Refuses to let the application start with a missing or weak signing key, or settings that make
/// no sense, so a mistake shows up at startup and not as a puzzling 401 later.
/// </summary>
public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            problems.Add($"{JwtSettings.SectionName}:SigningKey is missing. Set it in configuration or the Jwt__SigningKey environment variable.");
        }
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtSettings.MinimumKeyBytes)
        {
            problems.Add($"{JwtSettings.SectionName}:SigningKey must be at least {JwtSettings.MinimumKeyBytes} bytes long.");
        }

        if (options.LifetimeMinutes <= 0)
        {
            problems.Add($"{JwtSettings.SectionName}:LifetimeMinutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            problems.Add($"{JwtSettings.SectionName}:Issuer is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            problems.Add($"{JwtSettings.SectionName}:Audience is missing.");
        }

        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
