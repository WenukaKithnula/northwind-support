using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "alex.turner@northwind-support.example";
    private const string Password = "LocalDevOnly-Password-1";
    private const string StoredHash = "stored-hash";

    private static readonly DateTime Expiry = FixedClock.DefaultNow.AddMinutes(60);

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<Agent>())).Returns(new IssuedToken("the-token", Expiry));

        _handler = new LoginCommandHandler(_agents.Object, _hasher.Object, _tokens.Object);
    }

    private Agent AgentWithPassword(bool active = true, string? hash = StoredHash)
    {
        var agent = new Agent("Alex Turner", Email, 10, FixedClock.DefaultNow).WithId(1);

        if (hash is not null)
        {
            agent.SetPasswordHash(hash);
        }

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return agent;
    }

    private void PasswordIs(bool correct) =>
        _hasher.Setup(h => h.Verify(It.IsAny<string?>(), Password)).Returns(correct);

    private Task<LoginResponse> SignIn(string email = Email, string password = Password) =>
        _handler.HandleAsync(new LoginRequest(email, password), CancellationToken.None);

    [Fact]
    public async Task HandleAsync_WithTheRightPassword_ReturnsATokenItsExpiryAndWhoSignedIn()
    {
        AgentWithPassword();
        PasswordIs(true);

        var response = await SignIn();

        Assert.Equal("the-token", response.Token);
        Assert.Equal(Expiry, response.ExpiresAtUtc);
        Assert.Equal(1, response.Agent.Id);
        Assert.Equal("Alex Turner", response.Agent.FullName);
        Assert.Equal(Email, response.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_ChecksThePasswordAgainstTheStoredHash()
    {
        AgentWithPassword();
        PasswordIs(true);

        await SignIn();

        _hasher.Verify(h => h.Verify(StoredHash, Password), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_IgnoresSpacesAroundTheEmail()
    {
        AgentWithPassword();
        PasswordIs(true);

        var response = await SignIn(email: $"  {Email}  ");

        Assert.Equal("the-token", response.Token);
    }

    // Every way of failing gives the same answer, so nobody can tell which half was right.

    [Fact]
    public async Task HandleAsync_WithTheWrongPassword_IsUnauthorizedAndIssuesNoToken()
    {
        AgentWithPassword();
        PasswordIs(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownEmail_IsUnauthorizedAndIssuesNoToken()
    {
        _agents.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        PasswordIs(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn("nobody@example.test"));

        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForAnInactiveAgentEvenWithTheRightPassword_IsUnauthorizedAndIssuesNoToken()
    {
        AgentWithPassword(active: false);
        PasswordIs(true);

        await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ForAnAgentWithNoPassword_IsUnauthorizedAndIssuesNoToken()
    {
        AgentWithPassword(hash: null);
        PasswordIs(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_GivesTheSameMessageForEveryKindOfFailure()
    {
        // Wrong password.
        AgentWithPassword();
        PasswordIs(false);
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        // Unknown email.
        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((Agent?)null);
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        // Inactive.
        AgentWithPassword(active: false);
        PasswordIs(true);
        var inactive = await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        // No password set.
        AgentWithPassword(hash: null);
        PasswordIs(false);
        var noPassword = await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn());

        var messages = new[] { wrongPassword, unknownEmail, inactive, noPassword }.Select(e => e.Message).Distinct();

        Assert.Single(messages);
        Assert.Equal("Invalid email or password.", messages.Single());
    }

    [Fact]
    public async Task HandleAsync_StillChecksAPasswordWhenThereIsNoSuchAgent_SoTheTimeDoesNotGiveItAway()
    {
        _agents.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        PasswordIs(false);

        await Assert.ThrowsAsync<UnauthorizedException>(() => SignIn("nobody@example.test"));

        // The hasher is asked even with nothing to compare to, and spends the same time on it.
        _hasher.Verify(h => h.Verify(null, Password), Times.Once);
    }
}
