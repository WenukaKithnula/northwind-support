using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentTests
{
    private static Agent NewAgent() => new("Alex Turner", "alex@example.test", 10, FixedClock.DefaultNow);

    [Fact]
    public void ANewAgent_HasNoPasswordYet() =>
        Assert.Null(NewAgent().PasswordHash);

    [Fact]
    public void SetPasswordHash_StoresTheHash()
    {
        var agent = NewAgent();

        agent.SetPasswordHash("a-hash");

        Assert.Equal("a-hash", agent.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_RefusesAnEmptyHash(string hash) =>
        Assert.Throws<ArgumentException>(() => NewAgent().SetPasswordHash(hash));

    [Fact]
    public void SetPasswordHash_ReplacesAnEarlierHash()
    {
        var agent = NewAgent();
        agent.SetPasswordHash("first");

        agent.SetPasswordHash("second");

        Assert.Equal("second", agent.PasswordHash);
    }
}
