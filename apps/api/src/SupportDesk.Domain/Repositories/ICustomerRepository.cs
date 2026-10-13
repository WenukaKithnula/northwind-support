using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Customer"/> aggregates.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>The customer with their tier, or null when they do not exist.</summary>
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
}
