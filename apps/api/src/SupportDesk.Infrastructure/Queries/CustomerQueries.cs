using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Customers;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class CustomerQueries(SupportDbContext db, ITicketQueries tickets) : ICustomerQueries
{
    public async Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Customer>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Tier,
                db.Set<Ticket>().Where(t => t.CustomerId == c.Id).Count(OpenTicket.Predicate)))
            .ToListAsync(ct);

    public async Task<CustomerDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var customer = await db.Set<Customer>()
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Tier,
                c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (customer is null)
        {
            return null;
        }

        var customerTickets = await tickets.GetForCustomerAsync(id, ct);

        return new CustomerDetailDto(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Tier,
            customer.CreatedAtUtc,
            customerTickets);
    }
}
