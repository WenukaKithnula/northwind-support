using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Triage;

public class TicketTriageTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static TicketTriage TriageFor(IReadOnlyList<AgentCandidate> agents)
    {
        var repository = new Mock<IAgentRepository>();
        repository
            .Setup(r => r.GetCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        return new TicketTriage(repository.Object, TestSlaPolicy.Default);
    }

    private static Task<NewTicketDecision> Decide(
        Category category, Customer customer, TicketPriority? requested, IReadOnlyList<AgentCandidate>? agents = null) =>
        TriageFor(agents ?? SeededData.Agents)
            .DecideForNewTicketAsync(category, customer, requested, Now, CancellationToken.None);

    private static string AgentName(NewTicketDecision decision) =>
        SeededData.Agents.Single(agent => agent.Id == decision.AssignedAgentId).FullName;

    // The worked examples from the brief, against the seeded agents.

    [Fact]
    public async Task DecideForNewTicket_AdventureWorksOutageRequestedMedium_IsCriticalFor2HoursWithBen()
    {
        var decision = await Decide(SeededData.Outage, SeededData.AdventureWorks, TicketPriority.Medium);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
        Assert.Equal("Ben Osei", AgentName(decision));
        Assert.Equal(2, decision.Triage.SlaWindowHours);
    }

    [Fact]
    public async Task DecideForNewTicket_ContosoSecurityRequestedLow_IsCriticalFor2HoursWithPriya()
    {
        var decision = await Decide(SeededData.Security, SeededData.Contoso, TicketPriority.Low);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
        Assert.Equal("Priya Nair", AgentName(decision));

        // The wording the brief gives for this very case.
        Assert.Equal(TicketPriority.Critical, decision.Triage.AppliedPriority);
        Assert.Equal("Category 'Security' is always treated as Critical", decision.Triage.PriorityReason);
        Assert.Equal(2, decision.Triage.SlaWindowHours);
        Assert.Equal("Critical base window 4h, halved for a Premium customer", decision.Triage.SlaReason);
        Assert.Equal("Fewest open tickets among Security specialists", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_FabrikamBillingRequestedHigh_IsHighFor8HoursWithSara()
    {
        var decision = await Decide(SeededData.Billing, SeededData.Fabrikam, TicketPriority.High);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
        Assert.Equal("Sara Lindqvist", AgentName(decision));
        Assert.Equal("Priority High as requested", decision.Triage.PriorityReason);
        Assert.Equal("High base window 8h", decision.Triage.SlaReason);
        Assert.Equal("Fewest open tickets among Billing specialists", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_TailspinTechnicalWithNoPriority_IsMediumFor24HoursWithSara()
    {
        var decision = await Decide(SeededData.Technical, SeededData.Tailspin, requested: null);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal(Now.AddHours(24), decision.DueAtUtc);
        Assert.Equal("Sara Lindqvist", AgentName(decision));
        Assert.Equal("No priority requested, so the default Medium applies", decision.Triage.PriorityReason);
        Assert.Equal("Fewest open tickets among active agents with room", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_WideWorldGeneralRequestedLow_IsLowFor36HoursWithSara()
    {
        var decision = await Decide(SeededData.General, SeededData.WideWorld, TicketPriority.Low);

        Assert.Equal(TicketPriority.Low, decision.Priority);
        Assert.Equal(Now.AddHours(36), decision.DueAtUtc);
        Assert.Equal("Sara Lindqvist", AgentName(decision));
        Assert.Equal(36, decision.Triage.SlaWindowHours);
        Assert.Equal("Low base window 72h, halved for a Premium customer", decision.Triage.SlaReason);
    }

    // Nobody eligible is a normal outcome: the ticket is still created, unassigned, with a reason.

    [Fact]
    public async Task DecideForNewTicket_WithNoSpecialistForTheCategory_LeavesItUnassignedAndSaysWhy()
    {
        var decision = await Decide(SeededData.Security, SeededData.Contoso, TicketPriority.Low, agents: []);

        Assert.Null(decision.AssignedAgentId);
        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
        Assert.Equal("No available agent specialised in Security", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_WhenEverySpecialistIsFull_LeavesItUnassignedAndSaysWhy()
    {
        // Marcus (full) is the only Billing specialist left once Sara is out of the picture.
        var decision = await Decide(
            SeededData.Billing,
            SeededData.Fabrikam,
            TicketPriority.High,
            agents: SeededData.Agents.Where(agent => agent.FullName == "Marcus Doyle").ToList());

        Assert.Null(decision.AssignedAgentId);
        Assert.Equal("Every Billing specialist is at their open-ticket limit", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_ForACategoryWithoutASpecialistRule_SaysNoAgentIsAvailable()
    {
        var decision = await Decide(SeededData.General, SeededData.Fabrikam, TicketPriority.Low, agents: []);

        Assert.Null(decision.AssignedAgentId);
        Assert.Equal("No active agent is available", decision.Triage.AssignmentReason);
    }

    [Fact]
    public async Task DecideForNewTicket_WithAPremiumFloor_ExplainsTheMinimum()
    {
        var repository = new Mock<IAgentRepository>();
        repository
            .Setup(r => r.GetCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SeededData.Agents);

        // A one hour Critical window: half would be 30 minutes, the floor keeps it at an hour.
        var triage = new TicketTriage(repository.Object, TestSlaPolicy.Create(critical: 1));

        var decision = await triage.DecideForNewTicketAsync(
            SeededData.Security, SeededData.Contoso, TicketPriority.Low, Now, CancellationToken.None);

        Assert.Equal(Now.AddHours(1), decision.DueAtUtc);
        Assert.Equal(1, decision.Triage.SlaWindowHours);
        Assert.Contains("minimum", decision.Triage.SlaReason);
    }

    // After an escalation the owner is checked again.

    [Fact]
    public async Task ChooseOwnerAfterEscalation_KeepsTheCurrentAgentWhoStillQualifies()
    {
        var ticket = new TicketBuilder().AssignedTo(5).Build(); // Marcus: 6 of 6, but one of them is this ticket

        var decision = await TriageFor(SeededData.Agents)
            .ChooseOwnerAfterEscalationAsync(ticket, SeededData.Billing, CancellationToken.None);

        Assert.Equal(5, decision.AgentId);
        Assert.Equal("Marcus Doyle still qualifies, so keeps the ticket", decision.Reason);
    }

    [Fact]
    public async Task ChooseOwnerAfterEscalation_ReassignsWhenTheCurrentAgentIsInactive()
    {
        var ticket = new TicketBuilder().AssignedTo(6).Build(); // Yuki, who is inactive

        var decision = await TriageFor(SeededData.Agents)
            .ChooseOwnerAfterEscalationAsync(ticket, SeededData.Technical, CancellationToken.None);

        Assert.Equal(4, decision.AgentId); // Sara, the active agent with the fewest open tickets
        Assert.Equal("Fewest open tickets among active agents with room", decision.Reason);
    }

    [Fact]
    public async Task ChooseOwnerAfterEscalation_GivesAnUnassignedTicketAnOwnerIfOneQualifies()
    {
        var ticket = new TicketBuilder().AssignedTo(null).Build();

        var decision = await TriageFor(SeededData.Agents)
            .ChooseOwnerAfterEscalationAsync(ticket, SeededData.Outage, CancellationToken.None);

        Assert.Equal(2, decision.AgentId); // Ben
    }

    [Fact]
    public async Task ChooseOwnerAfterEscalation_WhenNobodyQualifies_ReturnsNobodyAndSaysWhy()
    {
        var ticket = new TicketBuilder().AssignedTo(1).Build();

        var decision = await TriageFor([])
            .ChooseOwnerAfterEscalationAsync(ticket, SeededData.Security, CancellationToken.None);

        Assert.Null(decision.AgentId);
        Assert.Equal("No available agent specialised in Security", decision.Reason);
    }
}
