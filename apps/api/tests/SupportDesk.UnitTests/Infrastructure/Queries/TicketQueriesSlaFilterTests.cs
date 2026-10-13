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
/// The SLA status filter against a real (in-memory SQLite) database. The status a row shows and
/// the status the filter selects by are worked out in two places - C# for the row, SQL for the
/// filter - so the main test here is that they never disagree.
/// </summary>
public sealed class TicketQueriesSlaFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly Customer _standard = new("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, Now);
    private readonly Customer _premium = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, Now);
    private readonly Category _category = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);

    public TicketQueriesSlaFilterTests()
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
    public async Task SlaStatus_NotApplicable_ReturnsTheTicketsWithoutADueDate()
    {
        var noDueDate = AddOpen(_standard, TicketPriority.High, dueAtUtc: null);
        AddOpen(_standard, TicketPriority.High, Now.AddHours(1));

        Assert.Equal([noDueDate], await Ids(SlaStatus.NotApplicable));
    }

    [Fact]
    public async Task SlaStatus_Breached_ReturnsOpenTicketsPastDueAndTicketsResolvedLate()
    {
        var pastDue = AddOpen(_standard, TicketPriority.High, Now.AddSeconds(-1));
        var resolvedLate = AddResolved(_standard, TicketPriority.High, due: Now.AddHours(-2), resolvedAt: Now.AddHours(-1));
        AddOpen(_standard, TicketPriority.High, Now); // exactly on the due date is not past it
        AddResolved(_standard, TicketPriority.High, due: Now.AddHours(-2), resolvedAt: Now.AddHours(-3));

        Assert.Equal([pastDue, resolvedLate], await Ids(SlaStatus.Breached));
    }

    [Fact]
    public async Task SlaStatus_Met_ReturnsTicketsResolvedOnOrBeforeTheirDueDate()
    {
        var onTime = AddResolved(_standard, TicketPriority.High, due: Now, resolvedAt: Now.AddHours(-1));
        var exactlyOnTime = AddResolved(_standard, TicketPriority.High, due: Now, resolvedAt: Now);
        AddResolved(_standard, TicketPriority.High, due: Now, resolvedAt: Now.AddSeconds(1));

        Assert.Equal([onTime, exactlyOnTime], await Ids(SlaStatus.Met));
    }

    [Fact]
    public async Task SlaStatus_AtRisk_IncludesExactlyAQuarterOfTheWindowAndExactlyTheDueDate()
    {
        // High is an 8 hour window for a Standard customer, so a quarter of it is 2 hours.
        var exactlyAtThreshold = AddOpen(_standard, TicketPriority.High, Now.AddHours(2));
        var dueRightNow = AddOpen(_standard, TicketPriority.High, Now);
        AddOpen(_standard, TicketPriority.High, Now.AddHours(2).AddSeconds(1));

        Assert.Equal([exactlyAtThreshold, dueRightNow], await Ids(SlaStatus.AtRisk));
    }

    [Fact]
    public async Task SlaStatus_WithinSla_IsOpenTicketsWithMoreThanAQuarterOfTheWindowLeft()
    {
        AddOpen(_standard, TicketPriority.High, Now.AddHours(2));
        var justOutside = AddOpen(_standard, TicketPriority.High, Now.AddHours(2).AddSeconds(1));
        var comfortable = AddOpen(_standard, TicketPriority.High, Now.AddHours(7));

        Assert.Equal([justOutside, comfortable], await Ids(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task SlaStatus_TheAtRiskThresholdDependsOnThePriorityAndTheTier()
    {
        // Critical is 4 hours, halved to 2 for Premium: a quarter is 30 minutes. For a Standard
        // customer it is a quarter of 4 hours: 1 hour.
        var premiumAtRisk = AddOpen(_premium, TicketPriority.Critical, Now.AddMinutes(30));
        var premiumFine = AddOpen(_premium, TicketPriority.Critical, Now.AddMinutes(31));
        var standardAtRisk = AddOpen(_standard, TicketPriority.Critical, Now.AddMinutes(31));
        var lowStandardFine = AddOpen(_standard, TicketPriority.Low, Now.AddHours(19)); // a quarter of 72h is 18h

        Assert.Equal([premiumAtRisk, standardAtRisk], await Ids(SlaStatus.AtRisk));
        Assert.Equal([premiumFine, lowStandardFine], await Ids(SlaStatus.WithinSla));
    }

    [Fact]
    public async Task EveryStatus_SelectsExactlyTheRowsThatShowThatStatus()
    {
        // Every priority and tier, at times either side of every boundary, open and resolved.
        TimeSpan[] offsets =
        [
            TimeSpan.FromHours(-50), TimeSpan.FromHours(-1), TimeSpan.FromSeconds(-1), TimeSpan.Zero,
            TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(31),
            TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)),
            TimeSpan.FromHours(4), TimeSpan.FromHours(6), TimeSpan.FromHours(9), TimeSpan.FromHours(18),
            TimeSpan.FromHours(19), TimeSpan.FromHours(50), TimeSpan.FromHours(100)
        ];

        foreach (var tier in new[] { _standard, _premium })
        {
            foreach (var priority in Enum.GetValues<TicketPriority>())
            {
                AddOpen(tier, priority, dueAtUtc: null);

                foreach (var offset in offsets)
                {
                    var due = Now + offset;

                    AddOpen(tier, priority, due);
                    AddResolved(tier, priority, due, resolvedAt: due.AddSeconds(-1));
                    AddResolved(tier, priority, due, resolvedAt: due);
                    AddResolved(tier, priority, due, resolvedAt: due.AddSeconds(1));
                }
            }
        }

        var allRows = await Rows(new TicketQuery());

        // Not a vacuous check: every one of the five statuses must actually occur.
        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            Assert.Contains(allRows, row => row.SlaStatus == status);
        }

        var total = 0;

        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            var shownAsThatStatus = allRows.Where(row => row.SlaStatus == status).Select(row => row.Id).Order().ToList();
            var selectedByTheFilter = (await Rows(new TicketQuery { SlaStatus = status })).Select(row => row.Id).Order().ToList();

            Assert.Equal(shownAsThatStatus, selectedByTheFilter);
            total += selectedByTheFilter.Count;
        }

        // Every ticket has exactly one status, so the five filtered sets add up to the whole table.
        Assert.Equal(allRows.Count, total);
    }

    [Fact]
    public async Task TheCountAndThePagesDescribeTheFilteredSet()
    {
        for (var i = 1; i <= 5; i++)
        {
            AddOpen(_standard, TicketPriority.High, Now.AddHours(-i)); // breached
        }

        AddOpen(_standard, TicketPriority.High, Now.AddHours(7)); // within
        AddOpen(_standard, TicketPriority.High, Now.AddHours(7));

        var page = await NewQueries().GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached, Page = 3, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task ItCombinesWithTheOtherFilters()
    {
        var breachedHigh = AddOpen(_standard, TicketPriority.High, Now.AddHours(-1));
        AddOpen(_standard, TicketPriority.Low, Now.AddHours(-1));
        AddOpen(_standard, TicketPriority.High, Now.AddHours(7));

        var rows = await Rows(new TicketQuery { SlaStatus = SlaStatus.Breached, Priority = TicketPriority.High });

        Assert.Equal([breachedHigh], rows.Select(row => row.Id).ToArray());
    }

    [Fact]
    public async Task ItSortsTheFilteredRows()
    {
        var latest = AddOpen(_standard, TicketPriority.High, Now.AddHours(-1));
        var earliest = AddOpen(_standard, TicketPriority.High, Now.AddHours(-5));
        var middle = AddOpen(_standard, TicketPriority.High, Now.AddHours(-3));

        var rows = await Rows(new TicketQuery
        {
            SlaStatus = SlaStatus.Breached,
            SortBy = "dueAtUtc",
            SortDirection = "asc"
        });

        Assert.Equal([earliest, middle, latest], rows.Select(row => row.Id).ToArray());
    }

    private TicketQueries NewQueries() => new(_db, new FixedClock(), TestSlaPolicy.Default);

    /// <summary>Every row a query returns, across all of its pages.</summary>
    private async Task<List<TicketListItemDto>> Rows(TicketQuery query)
    {
        var rows = new List<TicketListItemDto>();

        for (var page = 1; ; page++)
        {
            var result = await NewQueries().GetPagedAsync(
                (query with { Page = page, PageSize = TicketQuery.MaxPageSize }).Normalized(), CancellationToken.None);

            rows.AddRange(result.Items);

            if (page >= result.TotalPages)
            {
                return rows;
            }
        }
    }

    /// <summary>The ids of the tickets the filter selects, in the order they were created.</summary>
    private async Task<int[]> Ids(SlaStatus status) =>
        (await Rows(new TicketQuery { SlaStatus = status })).Select(row => row.Id).Order().ToArray();

    private int AddOpen(Customer customer, TicketPriority priority, DateTime? dueAtUtc) =>
        AddTicket(customer, priority, dueAtUtc, resolvedAtUtc: null);

    private int AddResolved(Customer customer, TicketPriority priority, DateTime due, DateTime resolvedAt) =>
        AddTicket(customer, priority, due, resolvedAt);

    private int AddTicket(Customer customer, TicketPriority priority, DateTime? dueAtUtc, DateTime? resolvedAtUtc)
    {
        var ticket = Ticket.Raise(
            $"TCK-{_db.Set<Ticket>().Count() + 1:D5}",
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
