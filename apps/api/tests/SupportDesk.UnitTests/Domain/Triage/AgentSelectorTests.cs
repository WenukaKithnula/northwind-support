using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectorTests
{
    private const int General = 1;
    private const int Billing = 2;
    private const int Technical = 3;
    private const int Outage = 4;
    private const int Security = 5;

    /// <summary>The six seeded agents, exactly as the brief's table lists them.</summary>
    private static readonly AgentCandidate[] SeededAgents =
    [
        Specialist(1, "Alex Turner", open: 5, max: 10, General, Technical),
        Specialist(2, "Ben Osei", open: 3, max: 8, Technical, Outage),
        Specialist(3, "Priya Nair", open: 4, max: 8, Outage, Security),
        Specialist(4, "Sara Lindqvist", open: 2, max: 6, General, Billing),
        Specialist(5, "Marcus Doyle", open: 6, max: 6, Billing, Security),
        Inactive(6, "Yuki Tanaka", open: 0, max: 8, Technical)
    ];

    // The worked examples from the brief, against the seeded agents.

    [Theory]
    [InlineData(Outage, true, "Ben Osei")]
    [InlineData(Security, true, "Priya Nair")]
    [InlineData(Billing, true, "Sara Lindqvist")]
    [InlineData(Technical, false, "Sara Lindqvist")]
    [InlineData(General, false, "Sara Lindqvist")]
    public void Select_OnTheSeededAgents_GivesTheAnswerTheBriefExpects(
        int categoryId, bool requiresSpecialist, string expectedAgent)
    {
        var selection = AgentSelector.Select(SeededAgents, categoryId, requiresSpecialist);

        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
        Assert.Equal(expectedAgent, selection.Agent?.FullName);
    }

    [Fact]
    public void Select_TheAgentWithTheFewestOpenTicketsWins()
    {
        var selection = AgentSelector.Select(
            [Agent(1, "A", open: 4), Agent(2, "B", open: 1), Agent(3, "C", open: 3)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WhenTheCategoryNeedsASpecialist_OnlySpecialistsAreConsidered()
    {
        // The agent with nothing open is not a Billing specialist, so cannot get it.
        var selection = AgentSelector.Select(
            [Agent(1, "Idle", open: 0), Specialist(2, "Specialist", open: 5, max: 10, Billing)],
            Billing,
            categoryRequiresSpecialist: true);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WhenTheCategoryNeedsNoSpecialist_AnyActiveAgentCanTakeIt()
    {
        var selection = AgentSelector.Select(
            [Agent(1, "No specialisations", open: 0)],
            Technical,
            categoryRequiresSpecialist: false);

        Assert.Equal(1, selection.Agent?.Id);
    }

    [Fact]
    public void Select_AnAgentAtTheirLimitIsExcluded()
    {
        var selection = AgentSelector.Select(
            [Agent(1, "Full", open: 6, max: 6), Agent(2, "Room", open: 5, max: 6)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_AnAgentOneBelowTheirLimitIsStillEligible()
    {
        var selection = AgentSelector.Select(
            [Agent(1, "Almost full", open: 5, max: 6)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(1, selection.Agent?.Id);
    }

    [Fact]
    public void Select_AnInactiveAgentIsNeverChosen()
    {
        var selection = AgentSelector.Select(
            [Inactive(1, "Inactive", open: 0, max: 10), Agent(2, "Active", open: 7)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void Select_WhenAgentsTie_TheLowestIdWinsWhateverTheOrder()
    {
        AgentCandidate[] agents = [Agent(7, "C", open: 2), Agent(3, "A", open: 2), Agent(5, "B", open: 2)];

        var forwards = AgentSelector.Select(agents, General, categoryRequiresSpecialist: false);
        var backwards = AgentSelector.Select(agents.Reverse(), General, categoryRequiresSpecialist: false);

        Assert.Equal(3, forwards.Agent?.Id);
        Assert.Equal(3, backwards.Agent?.Id);
    }

    [Fact]
    public void Select_WithNoSpecialistForTheCategory_ReturnsNobodyAndSaysWhy()
    {
        var selection = AgentSelector.Select(
            [Specialist(1, "A", open: 0, max: 10, General)],
            Security,
            categoryRequiresSpecialist: true);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.NoQualifiedAgent, selection.Outcome);
    }

    [Fact]
    public void Select_WhenEverySpecialistIsFull_ReturnsNobodyAndSaysWhy()
    {
        var selection = AgentSelector.Select(
            [Specialist(1, "Full specialist", open: 6, max: 6, Security)],
            Security,
            categoryRequiresSpecialist: true);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.AllQualifiedAgentsFull, selection.Outcome);
    }

    [Fact]
    public void Select_WithNoAgentsAtAll_ReturnsNobodyInsteadOfThrowing()
    {
        var selection = AgentSelector.Select([], General, categoryRequiresSpecialist: false);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.NoQualifiedAgent, selection.Outcome);
    }

    // After an escalation the owner is checked again: keep them if they still qualify.

    [Fact]
    public void KeepOrSelect_KeepsTheCurrentAgentWhoIsStillEligible_EvenWhenSomeoneElseHasFewerTickets()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1,
            [Agent(1, "Current", open: 5), Agent(2, "Quieter", open: 1)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(1, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.KeptCurrent, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_DoesNotCountTheEscalatedTicketAgainstItsOwnAgent()
    {
        // Marcus is at 6 of 6 only because he holds this very ticket.
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 5,
            SeededAgents,
            Billing,
            categoryRequiresSpecialist: true);

        Assert.Equal("Marcus Doyle", selection.Agent?.FullName);
        Assert.Equal(SelectionOutcome.KeptCurrent, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_ReassignsWhenTheCurrentAgentIsNoLongerActive()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1,
            [Inactive(1, "Gone", open: 1, max: 10), Agent(2, "Other", open: 4)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Equal(2, selection.Agent?.Id);
        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_ReassignsWhenTheCurrentAgentLacksTheSpecialization()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1,
            [
                Specialist(1, "Generalist", open: 1, max: 10, General),
                Specialist(2, "Specialist", open: 4, max: 10, Security)
            ],
            Security,
            categoryRequiresSpecialist: true);

        Assert.Equal(2, selection.Agent?.Id);
    }

    [Fact]
    public void KeepOrSelect_ForATicketWithNoOwner_SelectsLikeANewTicket()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: null,
            SeededAgents,
            Outage,
            categoryRequiresSpecialist: true);

        Assert.Equal("Ben Osei", selection.Agent?.FullName);
        Assert.Equal(SelectionOutcome.Selected, selection.Outcome);
    }

    [Fact]
    public void KeepOrSelect_WhenNobodyQualifiesAnyMore_ReturnsNobody()
    {
        var selection = AgentSelector.KeepOrSelect(
            currentAgentId: 1,
            [Inactive(1, "Gone", open: 1, max: 10)],
            General,
            categoryRequiresSpecialist: false);

        Assert.Null(selection.Agent);
        Assert.Equal(SelectionOutcome.NoQualifiedAgent, selection.Outcome);
    }

    /// <summary>An active agent with no specialisations.</summary>
    private static AgentCandidate Agent(int id, string name, int open, int max = 10) =>
        new(id, name, IsActive: true, max, open, new HashSet<int>());

    /// <summary>An active agent qualified for the given categories.</summary>
    private static AgentCandidate Specialist(int id, string name, int open, int max, params int[] categories) =>
        new(id, name, IsActive: true, max, open, categories.ToHashSet());

    /// <summary>An agent who no longer takes work.</summary>
    private static AgentCandidate Inactive(int id, string name, int open, int max, params int[] categories) =>
        new(id, name, IsActive: false, max, open, categories.ToHashSet());
}
