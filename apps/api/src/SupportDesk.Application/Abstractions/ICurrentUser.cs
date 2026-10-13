namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Who is making the current request, as proved by the token they sent. Anything that records
/// "who did this" takes it from here and never from what the caller put in a request body, which
/// could say anything.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in agent's id, or null when the request is not signed in.</summary>
    int? AgentId { get; }

    /// <summary>The signed-in agent's name, or null when the request is not signed in.</summary>
    string? FullName { get; }
}
