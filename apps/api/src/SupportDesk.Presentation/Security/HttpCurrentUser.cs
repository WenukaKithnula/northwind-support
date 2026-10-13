using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// The agent making the current request, read from the validated token. By the time anything asks,
/// the token's signature and expiry have already been checked.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public int? AgentId =>
        int.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    public string? FullName => User?.FindFirstValue(JwtRegisteredClaimNames.Name);
}
