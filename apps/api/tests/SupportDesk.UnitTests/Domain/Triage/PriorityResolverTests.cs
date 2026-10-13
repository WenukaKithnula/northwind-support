using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class PriorityResolverTests
{
    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Resolve_WhenNothingForcesCritical_UsesTheRequestedPriority(TicketPriority requested)
    {
        var decision = PriorityResolver.Resolve(requested, categoryForcesCritical: false);

        Assert.Equal(requested, decision.Priority);
        Assert.False(decision.ForcedByCategory);
        Assert.False(decision.WasDefaulted);
    }

    [Fact]
    public void Resolve_WhenNoPriorityWasRequested_DefaultsToMedium()
    {
        var decision = PriorityResolver.Resolve(requested: null, categoryForcesCritical: false);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.True(decision.WasDefaulted);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(null)]
    public void Resolve_WhenTheCategoryForcesCritical_IsAlwaysCritical(TicketPriority? requested)
    {
        var decision = PriorityResolver.Resolve(requested, categoryForcesCritical: true);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.True(decision.ForcedByCategory);
        Assert.False(decision.WasDefaulted);
    }
}
