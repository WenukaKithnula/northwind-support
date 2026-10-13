using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// Returns the agent making the request. A token proves who someone was when it was issued, so the
/// agent is looked up again: one who has since been deactivated or removed is no longer signed in.
/// </summary>
public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    private const string NotSignedIn = "You are not signed in.";

    /// <exception cref="UnauthorizedException">Nobody is signed in, or the agent can no longer sign in.</exception>
    public async Task<SignedInAgentDto> HandleAsync(CancellationToken ct)
    {
        var agentId = currentUser.AgentId
            ?? throw new UnauthorizedException(NotSignedIn);

        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException(NotSignedIn);
        }

        return new SignedInAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
