using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared arrangement for the ticket command tests: mocked repositories and queries, a fixed
/// clock, the real triage rules over the brief's seeded agents, and the command handlers wired to
/// all of them. A test only changes what it cares about.
/// </summary>
internal sealed class TicketCommandTestContext
{
    public TicketCommandTestContext()
    {
        // By default the customer is Standard and the category needs nothing special, so a ticket
        // keeps the priority it asked for.
        UseCustomer(SeededData.Fabrikam);
        UseCategory(SeededData.Technical);

        Agents.Setup(a => a.GetCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SeededData.Agents);

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        Tickets.Setup(t => t.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()))
            .Callback<TicketEscalation, CancellationToken>((escalation, _) => AddedEscalation = escalation)
            .Returns(Task.CompletedTask);

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        // The history as re-read once saved. The id is 0 because nothing here is really saved.
        TicketQueries.Setup(q => q.GetEscalationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TicketEscalationDto(
                    0, 41, TicketPriority.Low, TicketPriority.Medium, null, null, null, null,
                    "A reason", "alex.lead", FixedClock.DefaultNow)
            ]);

        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);
        var getEscalations = new GetTicketEscalationsQueryHandler(TicketQueries.Object);
        var triage = new TicketTriage(Agents.Object, SlaPolicy);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, triage, UnitOfWork.Object, Clock, getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        EscalateTicket = new EscalateTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, triage, SlaPolicy, UnitOfWork.Object, Clock,
            getTicket, getEscalations, NullLogger<EscalateTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(Tickets.Object, UnitOfWork.Object, Clock, getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object, Agents.Object, UnitOfWork.Object, Clock, getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public SlaPolicy SlaPolicy { get; } = TestSlaPolicy.Default;

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public EscalateTicketCommandHandler EscalateTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>The history row handed to the repository by the last escalate call.</summary>
    public TicketEscalation? AddedEscalation { get; private set; }

    /// <summary>Makes the customer lookup return this customer, whatever id is asked for.</summary>
    public void UseCustomer(Customer customer) =>
        Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

    /// <summary>Makes the category lookup return this category, whatever id is asked for.</summary>
    public void UseCategory(Category category) =>
        Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

    /// <summary>Makes <see cref="ITicketRepository.GetByIdAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true)
    {
        var agent = new Agent(fullName, $"agent{id}@example.com", maxOpenTickets: 10, Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}
