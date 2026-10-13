using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, and triages it: the priority, the due date and the owner are
/// decided by the triage rules, and the response says how.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var decision = await triage.DecideForNewTicketAsync(
            category, customer, request.RequestedPriority, now, ct);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            customer.Id,
            category.Id,
            decision.Priority,
            now,
            decision.DueAtUtc);

        // Nobody eligible is a normal outcome: the ticket is raised without an owner.
        if (decision.AssignedAgentId is { } agentId)
        {
            ticket.AssignTo(agentId, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, owner {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId);

        var created = await getTicket.HandleAsync(ticket.Id, ct);
        created.Triage = decision.Triage;

        return created;
    }
}
