using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    private static readonly SlaPolicy Policy = TestSlaPolicy.Default;

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void WindowFor_AStandardCustomer_IsTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Standard));

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void WindowFor_APremiumCustomer_IsHalfTheBaseWindow(TicketPriority priority, double hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), Policy.WindowFor(priority, CustomerTier.Premium));

    [Fact]
    public void WindowFor_APremiumCustomerWithAOneHourBase_KeepsTheOneHourFloor()
    {
        var policy = TestSlaPolicy.Create(critical: 1);

        // Half of one hour is 30 minutes; the floor says never below an hour.
        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_APremiumCustomer_IsRaisedToTheFloorWhenHalfIsBelowIt()
    {
        var policy = TestSlaPolicy.Create(critical: 1.5);

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_APremiumCustomer_IsNeverLongerThanTheStandardWindow()
    {
        var policy = TestSlaPolicy.Create(critical: 0.5);

        Assert.Equal(TimeSpan.FromHours(0.5), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_FollowsTheConfiguredNumbers()
    {
        var policy = TestSlaPolicy.Create(high: 10, premiumMultiplier: 0.25, minimumHours: 1);

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.High, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(2.5), policy.WindowFor(TicketPriority.High, CustomerTier.Premium));
    }

    [Fact]
    public void DueAt_IsTheStartPlusTheWindow() =>
        Assert.Equal(
            FixedClock.DefaultNow.AddHours(2),
            Policy.DueAt(FixedClock.DefaultNow, TicketPriority.Critical, CustomerTier.Premium));

    [Theory]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 120)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 30)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 1080)]
    public void AtRiskSpanFor_IsTheThresholdShareOfTheWindow(
        TicketPriority priority, CustomerTier tier, double minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), Policy.AtRiskSpanFor(priority, tier));

    [Fact]
    public void Constructor_WithoutAWindowForAPriority_Throws()
    {
        var windows = new Dictionary<TicketPriority, double>
        {
            [TicketPriority.Critical] = 4,
            [TicketPriority.High] = 8,
            [TicketPriority.Medium] = 24
        };

        var exception = Assert.Throws<ArgumentException>(() => new SlaPolicy(windows, 0.5, 1, 25));

        Assert.Contains("Low", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Constructor_WithAWindowOfZeroOrLess_Throws(double hours) =>
        Assert.Throws<ArgumentException>(() => TestSlaPolicy.Create(critical: hours));

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Constructor_WithAPremiumMultiplierOutsideZeroToOne_Throws(double multiplier) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSlaPolicy.Create(premiumMultiplier: multiplier));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_WithAnAtRiskThresholdOutsideZeroToOneHundred_Throws(double percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSlaPolicy.Create(atRiskPercent: percent));

    [Fact]
    public void Constructor_WithANegativeFloor_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TestSlaPolicy.Create(minimumHours: -1));
}
