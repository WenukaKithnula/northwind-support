using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }

    // Triage: the decision is applied to the ticket, and explained in the response.

    [Fact]
    public async Task HandleAsync_ASecurityTicketIsCriticalHoweverItWasRequested()
    {
        var context = new TicketCommandTestContext();
        context.UseCategory(SeededData.Security);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
    }

    [Fact]
    public async Task HandleAsync_SetsTheDueDateFromTheWindowForThePriorityAndTier()
    {
        var context = new TicketCommandTestContext();
        context.UseCustomer(SeededData.Contoso); // Premium
        context.UseCategory(SeededData.Security);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        // Critical is 4 hours, halved for Premium.
        Assert.Equal(FixedClock.DefaultNow.AddHours(2), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheEligibleAgentWithTheFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        context.UseCategory(SeededData.Security);

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(3, context.AddedTicket!.AssignedAgentId); // Priya; Marcus is full
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_StillCreatesTheTicketUnassignedWithAReason()
    {
        var context = new TicketCommandTestContext();
        context.UseCategory(SeededData.Security);
        context.Agents
            .Setup(a => a.GetCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var created = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        context.VerifySaved(Times.Once());
        Assert.Equal("No available agent specialised in Security", created.Triage?.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheTriageDecisionWithTheTicket()
    {
        var context = new TicketCommandTestContext();
        context.UseCustomer(SeededData.Contoso);
        context.UseCategory(SeededData.Security);

        var created = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        var triage = created.Triage;
        Assert.NotNull(triage);
        Assert.Equal(TicketPriority.Critical, triage.AppliedPriority);
        Assert.Equal("Category 'Security' is always treated as Critical", triage.PriorityReason);
        Assert.Equal(2, triage.SlaWindowHours);
        Assert.Equal("Critical base window 4h, halved for a Premium customer", triage.SlaReason);
        Assert.Equal("Fewest open tickets among Security specialists", triage.AssignmentReason);
    }
}
