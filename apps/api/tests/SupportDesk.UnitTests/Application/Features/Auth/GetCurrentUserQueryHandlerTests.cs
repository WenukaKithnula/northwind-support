using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private GetCurrentUserQueryHandler Handler() => new(_currentUser.Object, _agents.Object);

    private Agent ExistingAgent(bool active = true)
    {
        var agent = new Agent("Alex Turner", "alex@example.test", 10, FixedClock.DefaultNow).WithId(7);

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return agent;
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheSignedInAgent()
    {
        _currentUser.SetupGet(u => u.AgentId).Returns(7);
        ExistingAgent();

        var me = await Handler().HandleAsync(CancellationToken.None);

        Assert.Equal(7, me.Id);
        Assert.Equal("Alex Turner", me.FullName);
        Assert.Equal("alex@example.test", me.Email);
    }

    [Fact]
    public async Task HandleAsync_WithNobodySignedIn_IsUnauthorized()
    {
        _currentUser.SetupGet(u => u.AgentId).Returns((int?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenTheAgentHasSinceBeenDeactivated_IsUnauthorized()
    {
        // The token is still valid, but the agent behind it may no longer work here.
        _currentUser.SetupGet(u => u.AgentId).Returns(7);
        ExistingAgent(active: false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenTheAgentNoLongerExists_IsUnauthorized()
    {
        _currentUser.SetupGet(u => u.AgentId).Returns(7);
        _agents.Setup(a => a.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().HandleAsync(CancellationToken.None));
    }
}
