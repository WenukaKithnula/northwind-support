using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner. The due date is whatever the SLA policy worked out, or null when none applies.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc,
        DateTime? dueAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            DueAtUtc = dueAtUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Raises the priority one level, restarts the due date from this moment under the new
    /// priority, hands the ticket to <paramref name="newAgentId"/> and returns the history row that
    /// records the change. Who the new owner should be is for the caller to decide, because it can
    /// see the agents; the ticket applies what it is given.
    /// </summary>
    /// <param name="reason">Why, already checked against the length limits by the caller.</param>
    /// <param name="escalatedBy">Who is escalating it.</param>
    /// <param name="policy">The SLA rules, so the new due date comes from the same place as every other.</param>
    /// <param name="customerTier">The ticket's customer's tier, which the window depends on.</param>
    /// <param name="newAgentId">The owner afterwards: the same agent, another, or null for nobody.</param>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is Resolved or Closed, or is already Critical.
    /// </exception>
    public TicketEscalation Escalate(
        string reason,
        string escalatedBy,
        SlaPolicy policy,
        CustomerTier customerTier,
        int? newAgentId,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);
        ArgumentNullException.ThrowIfNull(policy);

        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated further.");
        }

        var raisedPriority = (TicketPriority)((int)Priority + 1);
        var newDueAtUtc = policy.DueAt(nowUtc, raisedPriority, customerTier);

        var escalation = new TicketEscalation(
            Id,
            Priority,
            raisedPriority,
            AssignedAgentId,
            newAgentId,
            DueAtUtc,
            newDueAtUtc,
            reason.Trim(),
            escalatedBy.Trim(),
            nowUtc);

        Priority = raisedPriority;
        DueAtUtc = newDueAtUtc;
        AssignedAgentId = newAgentId;
        UpdatedAtUtc = nowUtc;

        return escalation;
    }
}
