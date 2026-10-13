namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Who is signed in: just enough for the screen to greet them.</summary>
public sealed record SignedInAgentDto(int Id, string FullName, string Email);

/// <summary>What a successful sign-in returns: the token, when it expires and who it is for.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, SignedInAgentDto Agent);
