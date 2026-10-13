using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// One query for every agent: their limit, their specializations and the number of tickets they
    /// hold that are still open. The count is a subquery per agent inside the one statement, so
    /// there is no query per agent.
    /// </summary>
    public async Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct)
    {
        var rows = await db.Set<Agent>()
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                OpenTicketCount = db.Set<Ticket>()
                    .Where(t => t.AssignedAgentId == a.Id)
                    .Count(OpenTicket.Predicate),
                SpecializationCategoryIds = a.Specializations.Select(s => s.CategoryId).ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(row => new AgentCandidate(
                row.Id,
                row.FullName,
                row.IsActive,
                row.MaxOpenTickets,
                row.OpenTicketCount,
                row.SpecializationCategoryIds.ToHashSet()))
            .ToList();
    }
}
