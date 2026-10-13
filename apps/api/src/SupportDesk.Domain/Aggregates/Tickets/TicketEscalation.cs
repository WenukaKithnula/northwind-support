using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// One row of a ticket's escalation history: what the ticket looked like before and after it was
/// escalated, why, who did it and when. It is a record of something that happened, so nothing on
/// it can be changed once it has been written.
/// </summary>
/// <remarks>
/// Created only by <see cref="Ticket.Escalate"/>, so a row can never disagree with the change it
/// describes. The ticket and the agents are other aggregates and are referenced by id.
/// </remarks>
public sealed class TicketEscalation : Entity
{
    /// <summary>The shortest reason accepted, after trimming.</summary>
    public const int ReasonMinLength = 5;

    /// <summary>The longest reason accepted, after trimming.</summary>
    public const int ReasonMaxLength = 500;

    /// <summary>The longest name accepted for who escalated.</summary>
    public const int EscalatedByMaxLength = 100;

    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime? toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        if (reason.Length is < ReasonMinLength or > ReasonMaxLength)
        {
            throw new ArgumentException(
                $"The reason must be {ReasonMinLength} to {ReasonMaxLength} characters.", nameof(reason));
        }

        if (escalatedBy.Length > EscalatedByMaxLength)
        {
            throw new ArgumentException(
                $"Who escalated must be at most {EscalatedByMaxLength} characters.", nameof(escalatedBy));
        }

        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason;
        EscalatedBy = escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    /// <summary>The owner before, or null when the ticket had none.</summary>
    public int? FromAgentId { get; private set; }

    /// <summary>The owner after, or null when nobody was eligible.</summary>
    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime? ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Who escalated it: the signed-in agent once authentication exists.</summary>
    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
