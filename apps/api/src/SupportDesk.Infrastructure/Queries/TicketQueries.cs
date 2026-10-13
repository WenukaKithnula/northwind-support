using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy slaPolicy) : ITicketQueries
{
    /// <remarks>
    /// Filters, then counts, then sorts and pages, all in the database. The count therefore
    /// describes the filtered set, not every ticket.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var tickets = ApplyFilters(TicketsWithLabels(), query);

        var totalCount = await tickets.CountAsync(ct);

        var now = clock.UtcNow;
        var policy = slaPolicy;

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaStatusOf(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, x.Ticket.Priority, x.Customer.Tier, policy)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    /// <remarks>
    /// Newest first, with the agents' names joined in the same statement (no query per row). The
    /// index on (TicketId, EscalatedAtUtc DESC) serves exactly this read.
    /// </remarks>
    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AsNoTracking().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        return await (
            from escalation in db.Set<TicketEscalation>().AsNoTracking()
            where escalation.TicketId == ticketId
            from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
            from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
            orderby escalation.EscalatedAtUtc descending, escalation.Id descending
            select new TicketEscalationDto(
                escalation.Id,
                escalation.TicketId,
                escalation.FromPriority,
                escalation.ToPriority,
                fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var policy = slaPolicy;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaStatusOf(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, x.Ticket.Priority, x.Customer.Tier, policy)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var policy = slaPolicy;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaStatusOf(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, x.Ticket.Priority, x.Customer.Tier, policy)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// The SLA status shown on a row: derived from the dates, "now" and the at-risk span for the
    /// ticket's priority and the customer's tier, so the list and the detail never disagree.
    /// </summary>
    /// <remarks>
    /// Static, with the policy passed in: a client projection that reached for an instance member
    /// would make EF Core hold on to this whole object.
    /// </remarks>
    private static SlaStatus SlaStatusOf(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        TicketPriority priority,
        CustomerTier tier,
        SlaPolicy policy) =>
        SlaEvaluator.Evaluate(dueAtUtc, resolvedAtUtc, nowUtc, policy.AtRiskSpanFor(priority, tier));

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>
    /// Narrows the list to what the query asks for. Each filter that was supplied is one more
    /// <c>Where</c>, so they combine with AND, and a new filter is one more step here.
    /// </summary>
    private IQueryable<TicketWithLabels> ApplyFilters(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } assignedAgentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == assignedAgentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (query.SlaStatus is { } slaStatus)
        {
            source = source.Where(SlaStatusFilter.For(slaStatus, clock.UtcNow, slaPolicy));
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            source = source.Where(x =>
                x.Ticket.Title.Contains(search)
                || x.Ticket.Reference.Contains(search)
                || x.Customer.Name.Contains(search));
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }
}
