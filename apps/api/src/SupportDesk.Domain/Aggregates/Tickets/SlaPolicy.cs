using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// The response-window rules: how long a ticket may stay unresolved, and how close to its due
/// date it counts as at risk. The numbers are handed in (they come from configuration), so they
/// can change without touching code.
/// </summary>
/// <remarks>
/// This is the one place the windows are worked out. Raising a ticket and escalating one both
/// ask it, so the two can never disagree.
/// </remarks>
public sealed class SlaPolicy
{
    private readonly Dictionary<TicketPriority, double> _baseWindowHours;
    private readonly double _premiumCustomerMultiplier;
    private readonly double _minimumWindowHours;

    public SlaPolicy(
        IReadOnlyDictionary<TicketPriority, double> baseWindowHours,
        double premiumCustomerMultiplier,
        double minimumWindowHours,
        double atRiskThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(baseWindowHours);

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!baseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                throw new ArgumentException(
                    $"A base window greater than zero is needed for priority {priority}.",
                    nameof(baseWindowHours));
            }
        }

        if (premiumCustomerMultiplier is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(premiumCustomerMultiplier),
                premiumCustomerMultiplier,
                "The premium multiplier must be above 0 and at most 1.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minimumWindowHours);

        if (atRiskThresholdPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atRiskThresholdPercent),
                atRiskThresholdPercent,
                "The at-risk threshold must be between 0 and 100.");
        }

        _baseWindowHours = new Dictionary<TicketPriority, double>(baseWindowHours);
        _premiumCustomerMultiplier = premiumCustomerMultiplier;
        _minimumWindowHours = minimumWindowHours;
        AtRiskThresholdPercent = atRiskThresholdPercent;
    }

    /// <summary>A ticket is at risk once this share of its window, or less, is left.</summary>
    public double AtRiskThresholdPercent { get; }

    /// <summary>The share of the standard window a Premium customer gets.</summary>
    public double PremiumCustomerMultiplier => _premiumCustomerMultiplier;

    /// <summary>The shortest window a Premium customer is ever given, in hours.</summary>
    public double MinimumWindowHours => _minimumWindowHours;

    /// <summary>The standard window for a priority, before any Premium reduction.</summary>
    public TimeSpan BaseWindowFor(TicketPriority priority) => TimeSpan.FromHours(_baseWindowHours[priority]);

    /// <summary>
    /// How long a ticket of this priority may stay open for a customer of this tier. A Premium
    /// customer gets the base window scaled down, but never below the minimum - and never longer
    /// than the standard window, which a minimum larger than the base window would otherwise cause.
    /// </summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var baseHours = _baseWindowHours[priority];

        var hours = tier == CustomerTier.Premium
            ? Math.Min(baseHours, Math.Max(baseHours * _premiumCustomerMultiplier, _minimumWindowHours))
            : baseHours;

        return TimeSpan.FromHours(hours);
    }

    /// <summary>When a ticket whose clock starts at <paramref name="startUtc"/> is due.</summary>
    public DateTime DueAt(DateTime startUtc, TicketPriority priority, CustomerTier tier) =>
        startUtc + WindowFor(priority, tier);

    /// <summary>How much time left, or less, makes a ticket of this kind at risk.</summary>
    public TimeSpan AtRiskSpanFor(TicketPriority priority, CustomerTier tier) =>
        WindowFor(priority, tier) * (AtRiskThresholdPercent / 100);
}
