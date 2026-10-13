using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA status on a ticket row comes from the dates, "now" and the at-risk span for the
/// ticket's priority and the customer's tier. This runs the real queries to check the list and
/// the detail screen both show it, At risk included.
/// </summary>
public sealed class TicketQueriesSlaStatusTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly Customer _standard = new("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, Now);
    private readonly Customer _premium = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, Now);
    private readonly Category _category = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);

    public TicketQueriesSlaStatusTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();
        _db.AddRange(_standard, _premium, _category);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_AHighTicketWithAQuarterOfItsWindowLeft_IsAtRisk()
    {
        // High is an 8 hour window for a Standard customer, so 2 hours left is exactly 25%.
        var id = AddTicket(_standard, TicketPriority.High, dueAtUtc: Now.AddHours(2));

        var row = await ListRow(id);

        Assert.Equal(SlaStatus.AtRisk, row.SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_TheSameDueDateIsOnlyWithinSlaForALongerWindow()
    {
        // Low is a 72 hour window, so a quarter is 18 hours: 2 hours left is at risk, 19 is not.
        var atRisk = AddTicket(_standard, TicketPriority.Low, dueAtUtc: Now.AddHours(2));
        var comfortable = AddTicket(_standard, TicketPriority.Low, dueAtUtc: Now.AddHours(19));

        Assert.Equal(SlaStatus.AtRisk, (await ListRow(atRisk)).SlaStatus);
        Assert.Equal(SlaStatus.WithinSla, (await ListRow(comfortable)).SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_APremiumCustomerHasAShorterWindowSoALowerThreshold()
    {
        // Critical is 4 hours, halved to 2 for Premium: a quarter is 30 minutes.
        var premiumAtRisk = AddTicket(_premium, TicketPriority.Critical, dueAtUtc: Now.AddMinutes(30));
        var premiumFine = AddTicket(_premium, TicketPriority.Critical, dueAtUtc: Now.AddMinutes(31));
        var standardAtRisk = AddTicket(_standard, TicketPriority.Critical, dueAtUtc: Now.AddMinutes(31));

        Assert.Equal(SlaStatus.AtRisk, (await ListRow(premiumAtRisk)).SlaStatus);
        Assert.Equal(SlaStatus.WithinSla, (await ListRow(premiumFine)).SlaStatus);
        Assert.Equal(SlaStatus.AtRisk, (await ListRow(standardAtRisk)).SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_ReportsBreachedMetAndNotApplicable()
    {
        var breached = AddTicket(_standard, TicketPriority.High, dueAtUtc: Now.AddMinutes(-1));
        var met = AddTicket(_standard, TicketPriority.High, dueAtUtc: Now.AddHours(1), resolvedAtUtc: Now);
        var noDueDate = AddTicket(_standard, TicketPriority.High, dueAtUtc: null);

        Assert.Equal(SlaStatus.Breached, (await ListRow(breached)).SlaStatus);
        Assert.Equal(SlaStatus.Met, (await ListRow(met)).SlaStatus);
        Assert.Equal(SlaStatus.NotApplicable, (await ListRow(noDueDate)).SlaStatus);
    }

    [Fact]
    public async Task GetDetailAsync_ShowsTheSameStatusAsTheList()
    {
        var id = AddTicket(_standard, TicketPriority.High, dueAtUtc: Now.AddHours(2));

        var detail = await NewQueries().GetDetailAsync(id, CancellationToken.None);

        Assert.Equal(SlaStatus.AtRisk, detail?.SlaStatus);
    }

    private TicketQueries NewQueries() => new(_db, new FixedClock(), TestSlaPolicy.Default);

    private async Task<TicketListItemDto> ListRow(int ticketId)
    {
        var page = await NewQueries().GetPagedAsync(new TicketQuery { PageSize = 100 }, CancellationToken.None);

        return page.Items.Single(item => item.Id == ticketId);
    }

    /// <summary>
    /// Adds a ticket with a due date. Nothing in the domain sets a due date or a resolution time
    /// to an arbitrary value, so they are written the way the seeder does it.
    /// </summary>
    private int AddTicket(Customer customer, TicketPriority priority, DateTime? dueAtUtc, DateTime? resolvedAtUtc = null)
    {
        var ticket = Ticket.Raise(
            $"TCK-{_db.Set<Ticket>().Count() + 1:D4}",
            "Something is not working",
            "A description long enough to be realistic.",
            customer.Id,
            _category.Id,
            priority,
            Now);

        if (resolvedAtUtc is not null)
        {
            ticket.ChangeStatus(TicketStatus.Resolved, resolvedAtUtc.Value);
        }

        var entry = _db.Add(ticket);
        entry.Property(t => t.DueAtUtc).CurrentValue = dueAtUtc;
        _db.SaveChanges();

        return ticket.Id;
    }
}
