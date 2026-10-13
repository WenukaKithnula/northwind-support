using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with their email and password and issues a token.
/// </summary>
/// <remarks>
/// Whatever goes wrong - no such email, the wrong password, an inactive agent, an agent with no
/// password - the answer is the same message, so nobody can tell which half of the credentials
/// was right. The password is checked even when there is nobody to check it against, so the time
/// taken does not give it away either.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    /// <exception cref="UnauthorizedException">The credentials were not accepted.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        var passwordMatches = passwordHasher.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !agent.IsActive || !passwordMatches)
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var token = tokenIssuer.Issue(agent);

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new SignedInAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
