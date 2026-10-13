using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static EscalateTicketRequest Request(string reason = "Customer called twice and needs an answer.") =>
        new(reason);

    [Fact]
    public async Task HandleAsync_RaisesThePriorityOneLevelAndSavesTheHistoryWithTheTicket()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).Build());

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(TicketPriority.Medium, context.AddedEscalation.FromPriority);
        Assert.Equal(TicketPriority.High, context.AddedEscalation.ToPriority);
        context.VerifySaved(Times.Once());
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Escalation);
    }

    [Fact]
    public async Task HandleAsync_RestartsTheDueDateFromNowForTheCustomersTier()
    {
        var context = new TicketCommandTestContext();
        context.UseCustomer(SeededData.Contoso); // Premium
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithPriority(TicketPriority.Low).DueAt(Now.AddHours(72)).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        // Medium is 24 hours, halved for Premium.
        Assert.Equal(Now.AddHours(12), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_KeepsTheCurrentAgentWhoIsStillEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(1).Build()); // Alex, 5 of 10

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(1, ticket.AssignedAgentId);
        Assert.Equal(1, context.AddedEscalation!.FromAgentId);
        Assert.Equal(1, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenTheCurrentAgentIsNoLongerEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(6).Build()); // Yuki, inactive

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId); // Sara: fewest open among the active agents
        Assert.Equal(6, context.AddedEscalation!.FromAgentId);
        Assert.Equal(4, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_RecordsTheReasonTrimmed()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        await context.EscalateTicket.HandleAsync(
            ticket.Id, new EscalateTicketRequest("  Whole site is down.  "), CancellationToken.None);

        Assert.Equal("Whole site is down.", context.AddedEscalation!.Reason);
        Assert.Equal(Now, context.AddedEscalation.EscalatedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_RecordsTheSignedInAgentAsWhoEscalatedIt()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs("Priya Nair");
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal("Priya Nair", context.AddedEscalation!.EscalatedBy);
    }

    [Fact]
    public async Task HandleAsync_WithNobodySignedIn_IsUnauthorizedAndSavesNothing()
    {
        var context = new TicketCommandTestContext();
        context.NobodySignedIn();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForATicketThatDoesNotExist_ThrowsNotFoundAndSavesNothing()
    {
        var context = new TicketCommandTestContext();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        Assert.Contains("Ticket", exception.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForACriticalTicket_IsRejectedAndSavesNothing()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_ForAResolvedOrClosedTicket_IsRejectedAndSaysWhich(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(status).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }
}
