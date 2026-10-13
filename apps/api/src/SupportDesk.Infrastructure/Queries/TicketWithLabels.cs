using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// A ticket together with the rows that label it. Tickets only hold their customer, category and
/// agent by id, so the list query joins them once and filters, sorts and projects from this.
/// </summary>
internal sealed class TicketWithLabels
{
    public required Ticket Ticket { get; init; }

    public required Customer Customer { get; init; }

    public required Category Category { get; init; }

    public Agent? Agent { get; init; }
}
