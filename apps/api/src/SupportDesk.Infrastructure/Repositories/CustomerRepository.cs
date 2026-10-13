using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CustomerRepository(SupportDbContext db) : ICustomerRepository
{
    /// <summary>Not tracked: triage only reads a customer's tier, it never changes the customer.</summary>
    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
