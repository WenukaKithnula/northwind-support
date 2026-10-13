using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>
/// The agents as the selection rules need them: how many tickets each holds that are still open,
/// and what each is qualified for, all read in one query.
/// </summary>
public sealed class AgentRepositoryCandidatesTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly Customer _customer = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Standard, Now);
    private readonly Category _billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
    private readonly Category _security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);

    public AgentRepositoryCandidatesTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();
        _db.AddRange(_customer, _billing, _security);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetCandidatesAsync_CountsOnlyTheTicketsThatAreStillOpen()
    {
        var agent = AddAgent("Sara Lindqvist", maxOpenTickets: 6);

        AddTicket(agent, TicketStatus.New);
        AddTicket(agent, TicketStatus.Open);
        AddTicket(agent, TicketStatus.InProgress);
        AddTicket(agent, TicketStatus.Resolved);
        AddTicket(agent, TicketStatus.Closed);

        var candidate = Assert.Single(await NewRepository().GetCandidatesAsync(CancellationToken.None));

        Assert.Equal(3, candidate.OpenTicketCount);
    }

    [Fact]
    public async Task GetCandidatesAsync_DoesNotCountOtherAgentsTickets()
    {
        var busy = AddAgent("Busy", maxOpenTickets: 8);
        var idle = AddAgent("Idle", maxOpenTickets: 8);
        AddTicket(busy, TicketStatus.Open);
        AddTicket(busy, TicketStatus.Open);

        var candidates = await NewRepository().GetCandidatesAsync(CancellationToken.None);

        Assert.Equal(2, candidates.Single(c => c.Id == busy.Id).OpenTicketCount);
        Assert.Equal(0, candidates.Single(c => c.Id == idle.Id).OpenTicketCount);
    }

    [Fact]
    public async Task GetCandidatesAsync_IgnoresTicketsWithNoOwner()
    {
        var agent = AddAgent("Sara Lindqvist", maxOpenTickets: 6);
        AddTicket(agent: null, TicketStatus.Open);

        var candidate = Assert.Single(await NewRepository().GetCandidatesAsync(CancellationToken.None));

        Assert.Equal(0, candidate.OpenTicketCount);
    }

    [Fact]
    public async Task GetCandidatesAsync_ReturnsTheSpecializationsLimitAndWhetherTheAgentIsActive()
    {
        var specialist = AddAgent("Priya Nair", maxOpenTickets: 8, _security, _billing);
        var inactive = AddAgent("Yuki Tanaka", maxOpenTickets: 5, deactivate: true);

        var candidates = await NewRepository().GetCandidatesAsync(CancellationToken.None);

        var priya = candidates.Single(c => c.Id == specialist.Id);
        Assert.True(priya.IsActive);
        Assert.Equal(8, priya.MaxOpenTickets);
        Assert.Equal(new HashSet<int> { _security.Id, _billing.Id }, priya.SpecializationCategoryIds);

        var yuki = candidates.Single(c => c.Id == inactive.Id);
        Assert.False(yuki.IsActive);
        Assert.Empty(yuki.SpecializationCategoryIds);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task TheOpenTicketPredicate_AgreesWithTheDomainForEveryStatus(TicketStatus status)
    {
        var agent = AddAgent("Sara Lindqvist", maxOpenTickets: 6);
        var ticket = AddTicket(agent, status);

        // The SQL predicate and Ticket.IsOpen are two spellings of one rule; they must not drift.
        var matchedInSql = await _db.Set<Ticket>()
            .Where(OpenTicket.Predicate)
            .AnyAsync(t => t.Id == ticket.Id);

        Assert.Equal(ticket.IsOpen, matchedInSql);
    }

    private AgentRepository NewRepository() => new(_db);

    private Agent AddAgent(string name, int maxOpenTickets, params Category[] specializations) =>
        AddAgent(name, maxOpenTickets, deactivate: false, specializations);

    private Agent AddAgent(string name, int maxOpenTickets, bool deactivate, params Category[] specializations)
    {
        var agent = new Agent(name, $"{name.Replace(' ', '.').ToLowerInvariant()}@example.test", maxOpenTickets, Now);

        foreach (var category in specializations)
        {
            agent.AddSpecialization(category.Id);
        }

        if (deactivate)
        {
            agent.Deactivate();
        }

        _db.Add(agent);
        _db.SaveChanges();

        return agent;
    }

    private Ticket AddTicket(Agent? agent, TicketStatus status)
    {
        var ticket = Ticket.Raise(
            $"TCK-{_db.Set<Ticket>().Count() + 1:D4}", "Something is not working",
            "A description long enough to be realistic.", _customer.Id, _billing.Id, TicketPriority.Medium, Now);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, Now);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, Now);
        }

        _db.Add(ticket);
        _db.SaveChanges();

        return ticket;
    }
}
