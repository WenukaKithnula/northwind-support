using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class IdentityPasswordHasherTests
{
    private static readonly IdentityPasswordHasher Hasher = new();

    private const string Password = "LocalDevOnly-Password-1";

    [Fact]
    public void Hash_IsNotThePasswordAndDoesNotContainIt()
    {
        var hash = Hasher.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void Verify_AcceptsTheRightPassword() =>
        Assert.True(Hasher.Verify(Hasher.Hash(Password), Password));

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("localdevonly-password-1")]
    [InlineData(" LocalDevOnly-Password-1")]
    [InlineData("")]
    public void Verify_RejectsAnyOtherPassword(string attempt) =>
        Assert.False(Hasher.Verify(Hasher.Hash(Password), attempt));

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferentlyEachTime()
    {
        var first = Hasher.Hash(Password);
        var second = Hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(Hasher.Verify(first, Password));
        Assert.True(Hasher.Verify(second, Password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_WithNothingToCheckAgainst_IsFalseForEveryPassword(string? noHash)
    {
        // An unknown email or an agent with no password has no hash. It must fail, and not throw.
        Assert.False(Hasher.Verify(noHash, Password));
        Assert.False(Hasher.Verify(noHash, ""));
    }

    [Fact]
    public void Verify_WithAHashThatIsNotOne_IsFalseInsteadOfThrowing() =>
        Assert.False(Hasher.Verify("not-a-real-hash", Password));

    [Fact]
    public void Hash_FitsTheColumnItIsStoredIn() =>
        Assert.True(Hasher.Hash(Password).Length <= 256);
}
