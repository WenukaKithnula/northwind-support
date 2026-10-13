using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>A signed token, and when it stops being accepted.</summary>
public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Signs a token that proves who an agent is, for the API to check on every later call.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Agent agent);
}
