using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Hashes passwords with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with a
/// random salt per password. No hashing is written here, on purpose; this only adapts a vetted
/// implementation to <see cref="IPasswordHasher"/>.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    // The hasher takes a user object it does not use.
    private static readonly object NoUser = new();

    private static readonly PasswordHasher<object> Hasher = new();

    // Checked against when there is no real hash, so a missing one costs as much time as a real one.
    private static readonly string DummyHash = Hasher.HashPassword(NoUser, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => Hasher.HashPassword(NoUser, password);

    public bool Verify(string? hash, string password)
    {
        var hasHash = !string.IsNullOrEmpty(hash);

        try
        {
            var result = Hasher.VerifyHashedPassword(NoUser, hasHash ? hash! : DummyHash, password);

            return hasHash && result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // What is stored is not a hash this hasher wrote: it matches nothing.
            return false;
        }
    }
}
