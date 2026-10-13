using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// One entry in a ticket's escalation history: what changed, why, who did it and when. The
/// agents are shown by name, so the screen never has to look them up.
/// </summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime? ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>The response to escalating a ticket: the ticket as it is now, and the history entry just written.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
