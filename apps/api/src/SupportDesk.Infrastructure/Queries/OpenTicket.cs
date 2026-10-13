using System.Linq.Expressions;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// "Open" as the database sees it: a ticket that is not Resolved and not Closed. It is
/// <see cref="Ticket.IsOpen"/> written as an expression EF Core can translate to SQL, so every
/// workload count in the application asks the same question, and a test keeps the two in step.
/// </summary>
public static class OpenTicket
{
    public static readonly Expression<Func<Ticket, bool>> Predicate =
        ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed;
}
