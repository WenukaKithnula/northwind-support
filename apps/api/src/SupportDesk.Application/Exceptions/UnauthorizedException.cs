namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when someone could not be identified, for example a sign-in with the wrong password.
/// Surfaces as HTTP 401. The message is shown to the caller, so it must never say which part of
/// the credentials was wrong.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
