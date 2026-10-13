using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>Finding an agent by the email they sign in with, and keeping the hash they sign in against.</summary>
public sealed class AgentRepositoryEmailTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;

    public AgentRepositoryEmailTests()
    {
        _connection.Open();

        _db = new SupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByEmailAsync_FindsTheAgentWithThatEmail()
    {
        var alex = AddAgent("Alex Turner", "alex@example.test");
        AddAgent("Ben Osei", "ben@example.test");

        var found = await new AgentRepository(_db).GetByEmailAsync("alex@example.test", CancellationToken.None);

        Assert.Equal(alex.Id, found?.Id);
        Assert.Equal("Alex Turner", found?.FullName);
    }

    [Fact]
    public async Task GetByEmailAsync_ForAnEmailNobodyHas_IsNull()
    {
        AddAgent("Alex Turner", "alex@example.test");

        Assert.Null(await new AgentRepository(_db).GetByEmailAsync("nobody@example.test", CancellationToken.None));
    }

    [Fact]
    public async Task TheHash_IsSavedAndReadBackWithTheAgent()
    {
        var agent = new Agent("Alex Turner", "alex@example.test", 10, FixedClock.DefaultNow);
        agent.SetPasswordHash("a-salted-hash");
        _db.Add(agent);
        _db.SaveChanges();

        var found = await new AgentRepository(_db).GetByEmailAsync("alex@example.test", CancellationToken.None);

        Assert.Equal("a-salted-hash", found?.PasswordHash);
    }

    [Fact]
    public async Task AnAgentWithoutAPassword_ReadsBackWithNoHash()
    {
        AddAgent("Alex Turner", "alex@example.test");

        var found = await new AgentRepository(_db).GetByEmailAsync("alex@example.test", CancellationToken.None);

        Assert.Null(found?.PasswordHash);
    }

    [Fact]
    public void TheModel_AllowsNoPasswordAndLimitsTheHash()
    {
        var property = _db.Model.FindEntityType(typeof(Agent))!.FindProperty(nameof(Agent.PasswordHash))!;

        Assert.True(property.IsNullable);
        Assert.Equal(256, property.GetMaxLength());
    }

    private Agent AddAgent(string name, string email)
    {
        var agent = new Agent(name, email, 10, FixedClock.DefaultNow);
        _db.Add(agent);
        _db.SaveChanges();

        return agent;
    }
}
