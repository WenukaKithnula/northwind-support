using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        // The length is counted after trimming, as the ticket will store it.
        RuleFor(x => x.Reason)
            .Must(reason => reason?.Trim().Length is >= TicketEscalation.ReasonMinLength and <= TicketEscalation.ReasonMaxLength)
            .WithMessage(
                $"The reason must be {TicketEscalation.ReasonMinLength} to {TicketEscalation.ReasonMaxLength} characters.");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty()
            .MaximumLength(TicketEscalation.EscalatedByMaxLength);
    }
}
