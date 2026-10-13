using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The agent who signs in with this email, or null when there is none.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Every agent as the selection rules see them: their limit, their qualifications and how many
    /// open tickets they hold now. Read in one query, however many agents there are.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct);
}
