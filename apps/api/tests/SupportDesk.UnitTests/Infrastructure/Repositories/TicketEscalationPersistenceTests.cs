using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Saves escalations to a real (in-memory SQLite) database, and checks the mapping the brief asks
/// to be configured properly: keys, lengths, required or optional columns and delete behaviour.
/// </summary>
public sealed class TicketEscalationPersistenceTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;

    private readonly Customer _customer = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, Now);
    private readonly Category _category = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
    private readonly Agent _alex = new("Alex Turner", "alex@example.test", 10, Now);
    private readonly Agent _sara = new("Sara Lindqvist", "sara@example.test", 6, Now);

    public TicketEscalationPersistenceTests()
    {
        _connection.Open();
        _db = NewContext();
        _db.Database.EnsureCreated();
        _db.AddRange(_customer, _category, _alex, _sara);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ATicketAndItsEscalation_AreSavedTogetherAndReadBackWithEveryField()
    {
        var ticketId = AddTicket(TicketPriority.Low, agent: _alex);
        var escalatedAt = Now.AddHours(5);

        // The way the escalate use case does it: load the ticket, escalate it, add the row, save once.
        var repository = new TicketRepository(_db);
        var ticket = await repository.GetByIdAsync(ticketId, CancellationToken.None);
        var escalation = ticket!.Escalate(
            "Customer called twice.", "alex.lead", TestSlaPolicy.Default, CustomerTier.Premium, _sara.Id, escalatedAt);
        await repository.AddEscalationAsync(escalation, CancellationToken.None);
        await _db.SaveChangesAsync();

        await using var fresh = NewContext();
        var savedTicket = await fresh.Set<Ticket>().SingleAsync(t => t.Id == ticketId);
        var saved = await fresh.Set<TicketEscalation>().SingleAsync();

        Assert.Equal(TicketPriority.Medium, savedTicket.Priority);
        Assert.Equal(_sara.Id, savedTicket.AssignedAgentId);
        Assert.NotEqual(0, saved.Id);
        Assert.Equal(ticketId, saved.TicketId);
        Assert.Equal(TicketPriority.Low, saved.FromPriority);
        Assert.Equal(TicketPriority.Medium, saved.ToPriority);
        Assert.Equal(_alex.Id, saved.FromAgentId);
        Assert.Equal(_sara.Id, saved.ToAgentId);
        Assert.Equal(escalatedAt.AddHours(12), saved.ToDueAtUtc);
        Assert.Equal("Customer called twice.", saved.Reason);
        Assert.Equal("alex.lead", saved.EscalatedBy);
        Assert.Equal(escalatedAt, saved.EscalatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.EscalatedAtUtc.Kind);
    }

    [Fact]
    public async Task AnEscalationWithNoAgentsAndNoDueDate_IsSavedWithNulls()
    {
        var ticketId = AddTicket(TicketPriority.Low, agent: null);
        await EscalateAndSave(ticketId, newAgentId: null);

        await using var fresh = NewContext();
        var saved = await fresh.Set<TicketEscalation>().SingleAsync();

        Assert.Null(saved.FromAgentId);
        Assert.Null(saved.ToAgentId);
        Assert.Null(saved.FromDueAtUtc);
        Assert.NotNull(saved.ToDueAtUtc);
    }

    [Fact]
    public async Task ATicketThatHasHistory_CannotBeDeleted()
    {
        var ticketId = AddTicket(TicketPriority.Low, agent: _alex);
        await EscalateAndSave(ticketId, _alex.Id);

        // A fresh context, as in a later request: it does not already track the history rows.
        await using var fresh = NewContext();
        var ticket = await fresh.Set<Ticket>().SingleAsync(t => t.Id == ticketId);
        fresh.Remove(ticket);

        await Assert.ThrowsAsync<DbUpdateException>(() => fresh.SaveChangesAsync());
    }

    [Fact]
    public async Task AnAgentNamedInTheHistory_CannotBeDeleted()
    {
        var ticketId = AddTicket(TicketPriority.Low, agent: _alex);
        await EscalateAndSave(ticketId, _sara.Id);

        // Sara is the new owner on the ticket and in the history row.
        await using var fresh = NewContext();
        var sara = await fresh.Set<Agent>().SingleAsync(a => a.Id == _sara.Id);
        fresh.Remove(sara);

        await Assert.ThrowsAsync<DbUpdateException>(() => fresh.SaveChangesAsync());
    }

    [Fact]
    public void TheModel_RequiresTheReasonAndWhoAndLimitsTheirLength()
    {
        var entity = EscalationEntity();

        Assert.Equal(TicketEscalation.ReasonMaxLength, entity.FindProperty(nameof(TicketEscalation.Reason))!.GetMaxLength());
        Assert.Equal(TicketEscalation.EscalatedByMaxLength, entity.FindProperty(nameof(TicketEscalation.EscalatedBy))!.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(TicketEscalation.Reason))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(TicketEscalation.EscalatedBy))!.IsNullable);
    }

    [Fact]
    public void TheModel_RequiresThePrioritiesAndTheTicketButAllowsNoAgentOrDueDate()
    {
        var entity = EscalationEntity();

        foreach (var required in new[]
        {
            nameof(TicketEscalation.TicketId),
            nameof(TicketEscalation.FromPriority),
            nameof(TicketEscalation.ToPriority),
            nameof(TicketEscalation.EscalatedAtUtc)
        })
        {
            Assert.False(entity.FindProperty(required)!.IsNullable, $"{required} should be required.");
        }

        foreach (var optional in new[]
        {
            nameof(TicketEscalation.FromAgentId),
            nameof(TicketEscalation.ToAgentId),
            nameof(TicketEscalation.FromDueAtUtc),
            nameof(TicketEscalation.ToDueAtUtc)
        })
        {
            Assert.True(entity.FindProperty(optional)!.IsNullable, $"{optional} should be optional.");
        }
    }

    [Fact]
    public void TheModel_NeverCascadesADeleteIntoTheHistory()
    {
        var foreignKeys = EscalationEntity().GetForeignKeys().ToList();

        Assert.Equal(3, foreignKeys.Count);
        Assert.All(foreignKeys, key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
    }

    [Fact]
    public void TheModel_HasAnIndexForOneTicketsHistoryNewestFirst()
    {
        // Sort order is only kept on the design-time model, the one migrations are built from.
        var designTimeEntity = _db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TicketEscalation))!;

        var index = designTimeEntity.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(TicketEscalation.TicketId), nameof(TicketEscalation.EscalatedAtUtc)]));

        Assert.Equal([false, true], index.IsDescending);
    }

    private SupportDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

    private IEntityType EscalationEntity() => _db.Model.FindEntityType(typeof(TicketEscalation))!;

    private int AddTicket(TicketPriority priority, Agent? agent)
    {
        var ticket = Ticket.Raise(
            "TCK-0001", "Something is not working", "A description long enough to be realistic.",
            _customer.Id, _category.Id, priority, Now);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, Now);
        }

        _db.Add(ticket);
        _db.SaveChanges();

        return ticket.Id;
    }

    private async Task EscalateAndSave(int ticketId, int? newAgentId)
    {
        var repository = new TicketRepository(_db);
        var ticket = await repository.GetByIdAsync(ticketId, CancellationToken.None);

        var escalation = ticket!.Escalate(
            "Customer called twice.", "alex.lead", TestSlaPolicy.Default, CustomerTier.Premium, newAgentId, Now.AddHours(1));

        await repository.AddEscalationAsync(escalation, CancellationToken.None);
        await _db.SaveChangesAsync();
    }
}
