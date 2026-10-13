using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Created = FixedClock.DefaultNow;
    private static readonly DateTime EscalatedAt = new(2026, 3, 2, 14, 0, 0, DateTimeKind.Utc);
    private static readonly SlaPolicy Policy = TestSlaPolicy.Default;

    private const string Reason = "Customer called twice and needs an answer.";
    private const string Actor = "alex.lead";

    private static TicketEscalation Escalate(
        Ticket ticket,
        CustomerTier tier = CustomerTier.Standard,
        int? newAgentId = null,
        DateTime? nowUtc = null,
        string reason = Reason,
        string escalatedBy = Actor) =>
        ticket.Escalate(reason, escalatedBy, Policy, tier, newAgentId, nowUtc ?? EscalatedAt);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalate_RaisesThePriorityOneLevel(TicketPriority before, TicketPriority after)
    {
        var ticket = new TicketBuilder().WithPriority(before).Build();

        Escalate(ticket);

        Assert.Equal(after, ticket.Priority);
    }

    [Fact]
    public void Escalate_AHighTicketAt1400ForAStandardCustomer_IsCriticalAndDueAt1800()
    {
        // The example from the brief: recomputed from the escalation, not from creation.
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.High)
            .DueAt(Created.AddHours(8))
            .Build();

        Escalate(ticket, CustomerTier.Standard);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(new DateTime(2026, 3, 2, 18, 0, 0, DateTimeKind.Utc), ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_ForAPremiumCustomer_UsesTheHalvedWindow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Escalate(ticket, CustomerTier.Premium);

        // Medium is 24 hours, halved to 12 for Premium.
        Assert.Equal(EscalatedAt.AddHours(12), ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_GivesATicketThatHadNoDueDateOne()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).DueAt(null).Build();

        var escalation = Escalate(ticket);

        Assert.Null(escalation.FromDueAtUtc);
        Assert.Equal(EscalatedAt.AddHours(8), escalation.ToDueAtUtc);
    }

    [Fact]
    public void Escalate_KeepsTheOwnerWhenTheSameAgentIsGiven()
    {
        var ticket = new TicketBuilder().AssignedTo(7).Build();

        var escalation = Escalate(ticket, newAgentId: 7);

        Assert.Equal(7, ticket.AssignedAgentId);
        Assert.Equal(7, escalation.FromAgentId);
        Assert.Equal(7, escalation.ToAgentId);
    }

    [Fact]
    public void Escalate_MovesTheTicketWhenAnotherAgentIsGiven()
    {
        var ticket = new TicketBuilder().AssignedTo(7).Build();

        var escalation = Escalate(ticket, newAgentId: 9);

        Assert.Equal(9, ticket.AssignedAgentId);
        Assert.Equal(7, escalation.FromAgentId);
        Assert.Equal(9, escalation.ToAgentId);
    }

    [Fact]
    public void Escalate_CanGiveAnUnassignedTicketAnOwner()
    {
        var ticket = new TicketBuilder().AssignedTo(null).Build();

        var escalation = Escalate(ticket, newAgentId: 4);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Null(escalation.FromAgentId);
        Assert.Equal(4, escalation.ToAgentId);
    }

    [Fact]
    public void Escalate_CanLeaveATicketUnassignedWhenNobodyIsEligible()
    {
        var ticket = new TicketBuilder().AssignedTo(7).Build();

        var escalation = Escalate(ticket, newAgentId: null);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(7, escalation.FromAgentId);
        Assert.Null(escalation.ToAgentId);
    }

    [Fact]
    public void Escalate_RecordsWhatChangedInTheHistoryRow()
    {
        var before = Created.AddHours(24);
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(7)
            .DueAt(before)
            .Build();

        var escalation = Escalate(ticket, newAgentId: 9, reason: "  Site is down for the whole office.  ", escalatedBy: "  alex.lead ");

        Assert.Equal(ticket.Id, escalation.TicketId);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(7, escalation.FromAgentId);
        Assert.Equal(9, escalation.ToAgentId);
        Assert.Equal(before, escalation.FromDueAtUtc);
        Assert.Equal(EscalatedAt.AddHours(8), escalation.ToDueAtUtc);
        Assert.Equal("Site is down for the whole office.", escalation.Reason);
        Assert.Equal("alex.lead", escalation.EscalatedBy);
        Assert.Equal(EscalatedAt, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_UpdatesTheTimeTheTicketWasLastChanged()
    {
        var ticket = new TicketBuilder().Build();

        Escalate(ticket);

        Assert.Equal(EscalatedAt, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_ACriticalTicket_IsRejectedAndSaysSo()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).DueAt(Created.AddHours(4)).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Contains("Critical", exception.Message);
        Assert.Contains(ticket.Reference, exception.Message);
        AssertUnchanged(ticket, TicketPriority.Critical, Created.AddHours(4));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_AResolvedOrClosedTicket_IsRejectedAndSaysWhich(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).DueAt(Created.AddHours(24)).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket));

        Assert.Contains(status.ToString(), exception.Message);
        AssertUnchanged(ticket, TicketPriority.Medium, Created.AddHours(24));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_WithoutAReason_Throws(string reason)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(() => Escalate(ticket, reason: reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_WithoutWhoEscalated_Throws(string escalatedBy)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(() => Escalate(ticket, escalatedBy: escalatedBy));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public void Escalate_AcceptsAReasonAtTheEdgesOfTheAllowedLength(int length)
    {
        var ticket = new TicketBuilder().Build();

        var escalation = Escalate(ticket, reason: new string('a', length));

        Assert.Equal(length, escalation.Reason.Length);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(501)]
    public void Escalate_RefusesAReasonJustOutsideTheAllowedLength_AndChangesNothing(int length)
    {
        var ticket = new TicketBuilder().DueAt(Created.AddHours(24)).Build();

        Assert.Throws<ArgumentException>(() => Escalate(ticket, reason: new string('a', length)));

        AssertUnchanged(ticket, TicketPriority.Medium, Created.AddHours(24));
    }

    [Fact]
    public void Escalate_CountsTheReasonAfterTrimming()
    {
        var ticket = new TicketBuilder().Build();

        // Only three real characters once the padding is gone.
        Assert.Throws<ArgumentException>(() => Escalate(ticket, reason: "   abc   "));
    }

    private static void AssertUnchanged(Ticket ticket, TicketPriority priority, DateTime dueAtUtc)
    {
        Assert.Equal(priority, ticket.Priority);
        Assert.Equal(dueAtUtc, ticket.DueAtUtc);
        Assert.Equal(Created, ticket.UpdatedAtUtc);
    }
}
