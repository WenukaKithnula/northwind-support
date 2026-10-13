using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

/// <summary>
/// The development seed gives every agent a password, stored only as a salted hash. It is the one
/// place a clear-text password exists in the code, and it is a local fixture.
/// </summary>
public sealed class SupportDbSeederCredentialsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly IdentityPasswordHasher _hasher = new();

    public SupportDbSeederCredentialsTests()
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

    private SupportDbSeeder NewSeeder() =>
        new(_db, new FixedClock(), _hasher, NullLogger<SupportDbSeeder>.Instance);

    private Task<List<Agent>> Agents() => _db.Set<Agent>().AsNoTracking().OrderBy(a => a.Id).ToListAsync();

    [Fact]
    public async Task Seeding_GivesEverySeededAgentAHashOfTheDevelopmentPassword()
    {
        await NewSeeder().SeedAsync();

        var agents = await Agents();

        Assert.Equal(6, agents.Count);
        Assert.All(agents, agent =>
        {
            Assert.NotNull(agent.PasswordHash);
            Assert.True(_hasher.Verify(agent.PasswordHash, SupportDbSeeder.DevelopmentPassword));
        });
    }

    [Fact]
    public async Task Seeding_NeverStoresThePasswordItself()
    {
        await NewSeeder().SeedAsync();

        Assert.All(await Agents(), agent =>
        {
            Assert.NotEqual(SupportDbSeeder.DevelopmentPassword, agent.PasswordHash);
            Assert.DoesNotContain(SupportDbSeeder.DevelopmentPassword, agent.PasswordHash!);
        });
    }

    [Fact]
    public async Task Seeding_SaltsEachAgentsHashSoTheSamePasswordLooksDifferent()
    {
        await NewSeeder().SeedAsync();

        var hashes = (await Agents()).Select(agent => agent.PasswordHash).Distinct().ToList();

        Assert.Equal(6, hashes.Count);
    }

    [Fact]
    public async Task Seeding_AWrongPasswordDoesNotVerifyForAnyAgent()
    {
        await NewSeeder().SeedAsync();

        Assert.All(await Agents(), agent => Assert.False(_hasher.Verify(agent.PasswordHash, "not-the-password")));
    }

    [Fact]
    public async Task Seeding_KeepsTheInactiveAgentSoTheyCanBeRefusedLater()
    {
        await NewSeeder().SeedAsync();

        var yuki = (await Agents()).Single(agent => agent.FullName == "Yuki Tanaka");

        Assert.False(yuki.IsActive);
        Assert.NotNull(yuki.PasswordHash);
    }

    [Fact]
    public async Task Seeding_AgainDoesNotChangeTheHashes()
    {
        await NewSeeder().SeedAsync();
        var before = (await Agents()).Select(agent => agent.PasswordHash).ToList();

        await NewSeeder().SeedAsync();
        var after = (await Agents()).Select(agent => agent.PasswordHash).ToList();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Seeding_GivesAPasswordToAgentsOfADatabaseSeededBeforePasswordsExisted()
    {
        await NewSeeder().SeedAsync();

        // Take the passwords away, as in a database that was seeded before this feature.
        await _db.Set<Agent>().ExecuteUpdateAsync(setters => setters.SetProperty(a => a.PasswordHash, (string?)null));
        _db.ChangeTracker.Clear();

        await NewSeeder().SeedAsync();

        Assert.All(await Agents(), agent =>
            Assert.True(_hasher.Verify(agent.PasswordHash, SupportDbSeeder.DevelopmentPassword)));
    }

    [Fact]
    public void TheDevelopmentPassword_IsObviouslyALocalFixture() =>
        Assert.Contains("LocalDev", SupportDbSeeder.DevelopmentPassword);
}
