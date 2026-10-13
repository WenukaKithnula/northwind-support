using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// What the system decided when a ticket was raised, and in plain words why, so nobody has to
/// reverse-engineer the rules from the result.
/// </summary>
public sealed record TriageDto(
    TicketPriority AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason);
