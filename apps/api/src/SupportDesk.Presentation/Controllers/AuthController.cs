using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Signs an agent in and returns a token.</summary>
    /// <response code="200">The token, when it expires and who it is for.</response>
    /// <response code="401">The email or password was not accepted. The message is the same whichever was wrong.</response>
    // The one endpoint that needs no token: everything else is closed by the fallback policy.
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The agent the token belongs to.</summary>
    [HttpGet("me")]
    public Task<SignedInAgentDto> Me([FromServices] GetCurrentUserQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(ct);
}
