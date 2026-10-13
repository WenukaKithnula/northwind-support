using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the ticket list against a real (in-memory SQLite) database. A mocked repository would
/// pass whatever the query looked like; here a filter only works if it becomes SQL, and the
/// count is the database's own count.
/// </summary>
/// <remarks>
/// SQLite compares text case-sensitively where SQL Server does not, so the search tests use
/// the exact case of the data.
/// </remarks>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;

    private readonly Customer _contoso = new("Contoso Ltd", "support@contoso.test", null, CustomerTier.Premium, Now);
    private readonly Customer _fabrikam = new("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, Now);
    private readonly Category _billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
    private readonly Category _technical = new("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
    private readonly Agent _alex = new("Alex Turner", "alex@example.test", 10, Now);
    private readonly Agent _sara = new("Sara Lindqvist", "sara@example.test", 6, Now);

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();

        _db.AddRange(_contoso, _fabrikam, _billing, _technical, _alex, _sara);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_PriorityIsAnExactMatch()
    {
        AddTicket("TCK-0001", priority: TicketPriority.High);
        AddTicket("TCK-0002", priority: TicketPriority.Low);
        AddTicket("TCK-0003", priority: TicketPriority.High);
        AddTicket("TCK-0004", priority: TicketPriority.Medium);

        var result = await Query(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_StatusIsAnExactMatch()
    {
        AddTicket("TCK-0001", status: TicketStatus.InProgress);
        AddTicket("TCK-0002", status: TicketStatus.Open);
        AddTicket("TCK-0003", status: TicketStatus.InProgress);

        var result = await Query(new TicketQuery { Status = TicketStatus.InProgress });

        Assert.Equal(["TCK-0003", "TCK-0001"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_CategoryIsAnExactMatch()
    {
        AddTicket("TCK-0001", category: _billing);
        AddTicket("TCK-0002", category: _technical);

        var result = await Query(new TicketQuery { CategoryId = _billing.Id });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_CustomerIsAnExactMatch()
    {
        AddTicket("TCK-0001", customer: _contoso);
        AddTicket("TCK-0002", customer: _fabrikam);

        var result = await Query(new TicketQuery { CustomerId = _fabrikam.Id });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_AssignedAgentIsAnExactMatch()
    {
        AddTicket("TCK-0001", agent: _alex);
        AddTicket("TCK-0002", agent: _sara);
        AddTicket("TCK-0003");

        var result = await Query(new TicketQuery { AssignedAgentId = _sara.Id });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnlyReturnsTicketsWithoutAnAgent()
    {
        AddTicket("TCK-0001", agent: _alex);
        AddTicket("TCK-0002");
        AddTicket("TCK-0003");

        var result = await Query(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(["TCK-0003", "TCK-0002"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_AnAgentAndUnassignedOnlyTogetherMatchNothing()
    {
        AddTicket("TCK-0001", agent: _alex);
        AddTicket("TCK-0002");

        var result = await Query(new TicketQuery { AssignedAgentId = _alex.Id, UnassignedOnly = true });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesFiltersWithAnd()
    {
        AddTicket("TCK-0001", customer: _contoso, category: _billing, priority: TicketPriority.High);
        AddTicket("TCK-0002", customer: _contoso, category: _technical, priority: TicketPriority.High);
        AddTicket("TCK-0003", customer: _fabrikam, category: _billing, priority: TicketPriority.High);
        AddTicket("TCK-0004", customer: _contoso, category: _billing, priority: TicketPriority.Low);

        var result = await Query(new TicketQuery
        {
            CustomerId = _contoso.Id,
            CategoryId = _billing.Id,
            Priority = TicketPriority.High
        });

        Assert.Equal(["TCK-0001"], References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesTheCustomerName()
    {
        AddTicket("TCK-0001", customer: _contoso, title: "Cannot log in");
        AddTicket("TCK-0002", customer: _fabrikam, title: "Cannot log in");

        var result = await Query(new TicketQuery { Search = "Contoso" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesThePartOfATitle()
    {
        AddTicket("TCK-0001", title: "Invoice total does not match");
        AddTicket("TCK-0002", title: "Cannot log in");

        var result = await Query(new TicketQuery { Search = "total" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_SearchMatchesTheReference()
    {
        AddTicket("TCK-0041");
        AddTicket("TCK-0042");
        AddTicket("TCK-0043");

        var result = await Query(new TicketQuery { Search = "TCK-0042" });

        Assert.Equal(["TCK-0042"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_SearchIsTrimmed()
    {
        AddTicket("TCK-0001", customer: _contoso);
        AddTicket("TCK-0002", customer: _fabrikam);

        var result = await Query(new TicketQuery { Search = "   Contoso  " });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetPagedAsync_ABlankSearchIsIgnored(string? search)
    {
        AddTicket("TCK-0001", customer: _contoso);
        AddTicket("TCK-0002", customer: _fabrikam);

        var result = await Query(new TicketQuery { Search = search });

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_ASearchWithNoMatchReturnsNothing()
    {
        AddTicket("TCK-0001");

        var result = await Query(new TicketQuery { Search = "nothing like this" });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchTreatsPercentAndUnderscoreAsPlainCharacters()
    {
        AddTicket("TCK-0001", title: "Uptime fell below 100% today");
        AddTicket("TCK-0002", title: "Uptime fell below 1000 today");
        AddTicket("TCK-0003", title: "Error in job_runner");
        AddTicket("TCK-0004", title: "Error in jobXrunner");

        var percent = await Query(new TicketQuery { Search = "100%" });
        var underscore = await Query(new TicketQuery { Search = "job_runner" });

        Assert.Equal(["TCK-0001"], References(percent));
        Assert.Equal(["TCK-0003"], References(underscore));
    }

    [Fact]
    public async Task GetPagedAsync_TheCountAndThePagesDescribeTheFilteredSet()
    {
        for (var i = 1; i <= 5; i++)
        {
            AddTicket($"TCK-000{i}", priority: TicketPriority.High);
        }

        AddTicket("TCK-0006", priority: TicketPriority.Low);
        AddTicket("TCK-0007", priority: TicketPriority.Low);

        var result = await Query(new TicketQuery { Priority = TicketPriority.High, Page = 3, PageSize = 2 });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task GetPagedAsync_APagePastTheEndIsEmptyAndTheCountIsStillRight()
    {
        AddTicket("TCK-0001", priority: TicketPriority.High);
        AddTicket("TCK-0002", priority: TicketPriority.High);
        AddTicket("TCK-0003", priority: TicketPriority.Low);

        var result = await Query(new TicketQuery { Priority = TicketPriority.High, Page = 5, PageSize = 20 });

        Assert.Empty(result.Items);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_WithoutFiltersReturnsEveryTicket()
    {
        AddTicket("TCK-0001");
        AddTicket("TCK-0002", priority: TicketPriority.High);

        var result = await Query(new TicketQuery());

        Assert.Equal(2, result.TotalCount);
    }

    private Task<PagedResult<TicketListItemDto>> Query(TicketQuery query) =>
        new TicketQueries(_db, new FixedClock(), TestSlaPolicy.Default).GetPagedAsync(query.Normalized(), CancellationToken.None);

    private static string[] References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(ticket => ticket.Reference).ToArray();

    /// <summary>Adds a ticket. Whatever the test does not mention gets a neutral default.</summary>
    private void AddTicket(
        string reference,
        Customer? customer = null,
        Category? category = null,
        TicketPriority priority = TicketPriority.Medium,
        TicketStatus status = TicketStatus.New,
        Agent? agent = null,
        string title = "Something is not working")
    {
        var ticket = Ticket.Raise(
            reference,
            title,
            "A description long enough to be realistic.",
            (customer ?? _contoso).Id,
            (category ?? _technical).Id,
            priority,
            Now);

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
    }
}
