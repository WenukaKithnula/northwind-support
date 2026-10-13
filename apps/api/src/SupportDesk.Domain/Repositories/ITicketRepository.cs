using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads and stores <see cref="Ticket"/> aggregates. Changes are committed through
/// <see cref="IUnitOfWork"/>.
/// </summary>
public interface ITicketRepository
{
    /// <summary>Loads a tracked ticket for modification, or null when it does not exist.</summary>
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The next unused ticket reference, e.g. TCK-0041.</summary>
    Task<string> NextReferenceAsync(CancellationToken ct);

    Task AddAsync(Ticket ticket, CancellationToken ct);

    /// <summary>
    /// Adds the history row for an escalation. It is saved by the unit of work together with the
    /// change to the ticket, so the two cannot get out of step.
    /// </summary>
    Task AddEscalationAsync(TicketEscalation escalation, CancellationToken ct);
}
