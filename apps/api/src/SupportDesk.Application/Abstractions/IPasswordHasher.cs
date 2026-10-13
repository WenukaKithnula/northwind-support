namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into something safe to store, and checks a password against it later. The
/// hash is salted, so two agents with the same password have different hashes.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Whether <paramref name="password"/> matches <paramref name="hash"/>. A missing hash never
    /// matches, but still costs the same time to check, so how long a sign-in takes does not show
    /// whether the email exists.
    /// </summary>
    bool Verify(string? hash, string password);
}
