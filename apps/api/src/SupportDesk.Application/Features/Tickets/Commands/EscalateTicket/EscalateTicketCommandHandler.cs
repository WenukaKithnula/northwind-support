using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket: one priority level up, a due date restarted from now, the owner checked
/// again, and a history row recording who did it and why. The rules sit on the <c>Ticket</c> and in
/// the triage rules that raising a ticket uses too; this brings them together and saves the result.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriage triage,
    SlaPolicy slaPolicy,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    GetTicketEscalationsQueryHandler getEscalations,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="UnauthorizedException">Nobody is signed in.</exception>
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is Critical, Resolved or Closed.</exception>
    public async Task<EscalationResultDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        // Who escalated is whoever the token says, never what the caller wrote in the body.
        var escalatedBy = currentUser.FullName
            ?? throw new UnauthorizedException("You must be signed in to escalate a ticket.");

        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var owner = await triage.ChooseOwnerAfterEscalationAsync(ticket, category, ct);

        // The ticket refuses (Critical, Resolved, Closed) before anything is written.
        var escalation = ticket.Escalate(
            request.Reason, escalatedBy, slaPolicy, customer.Tier, owner.AgentId, clock.UtcNow);

        // The ticket and its history row go to the database in this one save.
        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated to {Priority} by {EscalatedBy}; owner {AgentId} ({OwnerReason}).",
            ticket.Reference, ticket.Priority, escalation.EscalatedBy, owner.AgentId, owner.Reason);

        var updated = await getTicket.HandleAsync(id, ct);
        var history = await getEscalations.HandleAsync(id, ct);

        return new EscalationResultDto(updated, history.Single(entry => entry.Id == escalation.Id));
    }
}
