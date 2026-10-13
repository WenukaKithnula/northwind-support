using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>What triage decided for a ticket being raised.</summary>
public sealed record NewTicketDecision(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AssignedAgentId,
    TriageDto Triage);

/// <summary>Who owns a ticket after an escalation, or null for nobody, and why.</summary>
public sealed record OwnerDecision(int? AgentId, string Reason);

/// <summary>
/// Applies the triage rules to a ticket. Raising a ticket and escalating one both come through
/// here, so the SLA windows, the priority rule and the choice of owner are worked out in exactly
/// one way. The rules themselves live in small classes in the domain; this brings them the data
/// they need (the agents and their workload) and puts the answer into words.
/// </summary>
public sealed class TicketTriage(IAgentRepository agents, SlaPolicy slaPolicy)
{
    /// <summary>Decides the priority, the due date and the owner of a ticket about to be raised.</summary>
    public async Task<NewTicketDecision> DecideForNewTicketAsync(
        Category category,
        Customer customer,
        TicketPriority? requestedPriority,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var priority = PriorityResolver.Resolve(requestedPriority, category.ForcesCriticalPriority);

        var window = slaPolicy.WindowFor(priority.Priority, customer.Tier);
        var dueAtUtc = slaPolicy.DueAt(nowUtc, priority.Priority, customer.Tier);

        var candidates = await agents.GetCandidatesAsync(ct);
        var selection = AgentSelector.Select(candidates, category.Id, category.RequiresSpecialist);

        var triage = new TriageDto(
            priority.Priority,
            TriageReasons.Priority(priority, category),
            window.TotalHours,
            TriageReasons.SlaWindow(slaPolicy, priority.Priority, customer.Tier),
            TriageReasons.Assignment(selection, category));

        return new NewTicketDecision(priority.Priority, dueAtUtc, selection.Agent?.Id, triage);
    }

    /// <summary>
    /// Decides who owns a ticket that is being escalated: its current agent while they still
    /// qualify, otherwise whoever would get it if it were new.
    /// </summary>
    public async Task<OwnerDecision> ChooseOwnerAfterEscalationAsync(
        Ticket ticket,
        Category category,
        CancellationToken ct)
    {
        var candidates = await agents.GetCandidatesAsync(ct);

        var selection = AgentSelector.KeepOrSelect(
            ticket.AssignedAgentId, candidates, category.Id, category.RequiresSpecialist);

        return new OwnerDecision(selection.Agent?.Id, TriageReasons.Assignment(selection, category));
    }
}
