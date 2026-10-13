using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>The history of a ticket's escalations, read back from a real (in-memory SQLite) database.</summary>
public sealed class TicketQueriesEscalationTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly Customer _customer = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Standard, Now);
    private readonly Category _category = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
    private readonly Agent _alex = new("Alex Turner", "alex@example.test", 10, Now);
    private readonly Agent _sara = new("Sara Lindqvist", "sara@example.test", 6, Now);

    public TicketQueriesEscalationTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

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
    public async Task GetEscalationsAsync_ReturnsTheNewestEscalationFirst()
    {
        var ticket = AddTicket(TicketPriority.Low, _alex);
        Escalate(ticket, Now.AddHours(1), _alex, "First reason");
        Escalate(ticket, Now.AddHours(3), _sara, "Second reason");
        // Written last, but happened between the other two: the order is by time, not by insertion.
        Escalate(ticket, Now.AddHours(2), _sara, "Out of order but earlier");

        var history = await NewQueries().GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Equal(
            ["Second reason", "Out of order but earlier", "First reason"],
            history.Select(entry => entry.Reason).ToArray());
    }

    [Fact]
    public async Task GetEscalationsAsync_BreaksATieOnTheTimeWithTheLatestRowFirst()
    {
        var ticket = AddTicket(TicketPriority.Low, _alex);
        Escalate(ticket, Now.AddHours(1), _alex, "Earlier row");
        Escalate(ticket, Now.AddHours(1), _alex, "Later row");

        var history = await NewQueries().GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.Equal(["Later row", "Earlier row"], history!.Select(entry => entry.Reason).ToArray());
    }

    [Fact]
    public async Task GetEscalationsAsync_ShowsBeforeAndAfterWithTheAgentNames()
    {
        var ticket = AddTicket(TicketPriority.Medium, _alex);
        var escalatedAt = Now.AddHours(2);
        Escalate(ticket, escalatedAt, _sara, "Needs a faster answer");

        var entry = Assert.Single((await NewQueries().GetEscalationsAsync(ticket.Id, CancellationToken.None))!);

        Assert.Equal(TicketPriority.Medium, entry.FromPriority);
        Assert.Equal(TicketPriority.High, entry.ToPriority);
        Assert.Equal("Alex Turner", entry.FromAgent?.FullName);
        Assert.Equal("Sara Lindqvist", entry.ToAgent?.FullName);
        Assert.Equal(escalatedAt.AddHours(8), entry.ToDueAtUtc);
        Assert.Equal("Needs a faster answer", entry.Reason);
        Assert.Equal("alex.lead", entry.EscalatedBy);
        Assert.Equal(escalatedAt, entry.EscalatedAtUtc);
    }

    [Fact]
    public async Task GetEscalationsAsync_ShowsNoAgentWhereTheTicketHadNoneBeforeOrAfter()
    {
        var ticket = AddTicket(TicketPriority.Low, owner: null);
        Escalate(ticket, Now.AddHours(1), newOwner: null, "Nobody is available");

        var entry = Assert.Single((await NewQueries().GetEscalationsAsync(ticket.Id, CancellationToken.None))!);

        Assert.Null(entry.FromAgent);
        Assert.Null(entry.ToAgent);
        Assert.Null(entry.FromDueAtUtc);
    }

    [Fact]
    public async Task GetEscalationsAsync_ForATicketNeverEscalated_IsAnEmptyList()
    {
        var ticket = AddTicket(TicketPriority.Low, _alex);

        var history = await NewQueries().GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetEscalationsAsync_ForATicketThatDoesNotExist_IsNull()
    {
        Assert.Null(await NewQueries().GetEscalationsAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task GetEscalationsAsync_OnlyShowsThatTicketsHistory()
    {
        var mine = AddTicket(TicketPriority.Low, _alex);
        var other = AddTicket(TicketPriority.Low, _alex);
        Escalate(mine, Now.AddHours(1), _alex, "Reason for this ticket");
        Escalate(other, Now.AddHours(1), _alex, "Reason for another ticket");

        var history = await NewQueries().GetEscalationsAsync(mine.Id, CancellationToken.None);

        Assert.Equal(["Reason for this ticket"], history!.Select(entry => entry.Reason).ToArray());
    }

    private TicketQueries NewQueries() => new(_db, new FixedClock(), TestSlaPolicy.Default);

    private Ticket AddTicket(TicketPriority priority, Agent? owner)
    {
        var ticket = Ticket.Raise(
            $"TCK-{_db.Set<Ticket>().Count() + 1:D4}", "Something is not working",
            "A description long enough to be realistic.", _customer.Id, _category.Id, priority, Now);

        if (owner is not null)
        {
            ticket.AssignTo(owner.Id, Now);
        }

        _db.Add(ticket);
        _db.SaveChanges();

        return ticket;
    }

    /// <summary>Escalates the ticket at the given time and saves the ticket and its history row.</summary>
    private void Escalate(Ticket ticket, DateTime at, Agent? newOwner, string reason)
    {
        var row = ticket.Escalate(
            reason, "alex.lead", TestSlaPolicy.Default, CustomerTier.Standard, newOwner?.Id, at);

        _db.Add(row);
        _db.SaveChanges();
    }
}
