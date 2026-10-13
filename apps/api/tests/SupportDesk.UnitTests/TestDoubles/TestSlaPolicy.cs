using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Builds an <see cref="SlaPolicy"/> for tests. The defaults are the numbers in the brief, so a
/// test only has to name what it wants to differ.
/// </summary>
public static class TestSlaPolicy
{
    public static SlaPolicy Default => Create();

    public static SlaPolicy Create(
        double critical = 4,
        double high = 8,
        double medium = 24,
        double low = 72,
        double premiumMultiplier = 0.5,
        double minimumHours = 1,
        double atRiskPercent = 25) =>
        new(
            new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Critical] = critical,
                [TicketPriority.High] = high,
                [TicketPriority.Medium] = medium,
                [TicketPriority.Low] = low
            },
            premiumMultiplier,
            minimumHours,
            atRiskPercent);
}
