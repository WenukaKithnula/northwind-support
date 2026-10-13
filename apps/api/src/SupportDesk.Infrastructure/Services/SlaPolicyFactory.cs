using System.Globalization;
using Microsoft.Extensions.Configuration;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Builds the <see cref="SlaPolicy"/> from the <c>Sla</c> configuration section: the base windows,
/// the premium multiplier, the minimum window and the at-risk threshold. Built once at startup, so
/// a missing or invalid value stops the application there instead of failing on some later request.
/// </summary>
public static class SlaPolicyFactory
{
    private const string SectionName = "Sla";

    public static SlaPolicy Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);

        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' configuration section is missing. It holds the SLA windows; see appsettings.json.");
        }

        var baseWindowHours = Enum.GetValues<TicketPriority>()
            .ToDictionary(priority => priority, priority => Read(section, $"BaseWindowHours:{priority}"));

        return new SlaPolicy(
            baseWindowHours,
            premiumCustomerMultiplier: Read(section, "PremiumCustomerMultiplier"),
            minimumWindowHours: Read(section, "MinimumWindowHours"),
            atRiskThresholdPercent: Read(section, "AtRiskThresholdPercent"));
    }

    private static double Read(IConfigurationSection section, string key)
    {
        var text = section[key];

        // Invariant culture, so "0.5" means a half on every machine.
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{key} is missing or is not a number (found '{text}').");
        }

        return value;
    }
}
