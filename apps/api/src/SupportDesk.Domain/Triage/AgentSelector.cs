namespace SupportDesk.Domain.Triage;

/// <summary>An agent as the selection rules see them: only the facts the rules need.</summary>
/// <param name="OpenTicketCount">Tickets that are not Resolved or Closed.</param>
/// <param name="SpecializationCategoryIds">The categories this agent is qualified for.</param>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlySet<int> SpecializationCategoryIds);

/// <summary>Why selection ended the way it did.</summary>
public enum SelectionOutcome
{
    /// <summary>An agent was chosen.</summary>
    Selected,

    /// <summary>The ticket's current agent is still eligible, so they keep it.</summary>
    KeptCurrent,

    /// <summary>No active agent has the specialization the category requires.</summary>
    NoQualifiedAgent,

    /// <summary>There are qualified agents, but every one is at their open-ticket limit.</summary>
    AllQualifiedAgentsFull
}

/// <summary>The result of choosing an owner. Having nobody to give it to is a normal outcome.</summary>
public sealed record AgentSelection(AgentCandidate? Agent, SelectionOutcome Outcome);

/// <summary>
/// Chooses the owner of a ticket. An agent is eligible when they are active, have the category's
/// specialization where the category requires one, and hold strictly fewer open tickets than their
/// limit. Of the eligible, the one with the fewest open tickets wins and the lowest id breaks a
/// tie, so the same input always gives the same answer.
/// </summary>
public static class AgentSelector
{
    /// <summary>Picks an owner for a ticket that needs one.</summary>
    public static AgentSelection Select(
        IEnumerable<AgentCandidate> candidates,
        int categoryId,
        bool categoryRequiresSpecialist)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var qualified = candidates
            .Where(agent => IsQualified(agent, categoryId, categoryRequiresSpecialist))
            .ToList();

        if (qualified.Count == 0)
        {
            return new AgentSelection(null, SelectionOutcome.NoQualifiedAgent);
        }

        var chosen = qualified
            .Where(HasRoom)
            .OrderBy(agent => agent.OpenTicketCount)
            .ThenBy(agent => agent.Id)
            .FirstOrDefault();

        return chosen is null
            ? new AgentSelection(null, SelectionOutcome.AllQualifiedAgentsFull)
            : new AgentSelection(chosen, SelectionOutcome.Selected);
    }

    /// <summary>
    /// Checks the owner again after an escalation: the current agent keeps the ticket while they
    /// are still eligible, otherwise it is given to someone as if it were new.
    /// </summary>
    /// <remarks>
    /// The ticket being escalated is already counted in its owner's open tickets. Counting it again
    /// would make an agent at their limit lose every ticket they already hold, so it is left out of
    /// the count when deciding whether they may keep it.
    /// </remarks>
    public static AgentSelection KeepOrSelect(
        int? currentAgentId,
        IEnumerable<AgentCandidate> candidates,
        int categoryId,
        bool categoryRequiresSpecialist)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var all = candidates.ToList();

        var current = currentAgentId is { } id ? all.FirstOrDefault(agent => agent.Id == id) : null;

        if (current is not null)
        {
            var withoutThisTicket = current with { OpenTicketCount = Math.Max(0, current.OpenTicketCount - 1) };

            if (IsQualified(withoutThisTicket, categoryId, categoryRequiresSpecialist) && HasRoom(withoutThisTicket))
            {
                return new AgentSelection(current, SelectionOutcome.KeptCurrent);
            }
        }

        return Select(all, categoryId, categoryRequiresSpecialist);
    }

    private static bool IsQualified(AgentCandidate agent, int categoryId, bool categoryRequiresSpecialist) =>
        agent.IsActive
        && (!categoryRequiresSpecialist || agent.SpecializationCategoryIds.Contains(categoryId));

    private static bool HasRoom(AgentCandidate agent) => agent.OpenTicketCount < agent.MaxOpenTickets;
}
