using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// Starts the real API in memory, with SQLite behind it and a signing key of the test's own, so a
/// test can call it over HTTP exactly as the web app does. Nothing here talks to SQL Server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-signing-key-at-least-32-bytes";
    public const string Password = "a-test-only-password";
    public const string ActiveEmail = "alex.turner@northwind-support.example";
    public const string InactiveEmail = "yuki.tanaka@northwind-support.example";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    static ApiFactory()
    {
        // Environment variables sit above appsettings files, so these win over the development values.
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Database__MigrateAndSeed", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, so Swagger is switched on and the tests can check it stays open.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();

            _connection.Open();
            services.AddDbContext<SupportDbContext>(ConfigureDatabase);
        });
    }

    /// <summary>The database the API is given. A test of an outage overrides it with one that is not there.</summary>
    protected virtual void ConfigureDatabase(DbContextOptionsBuilder options) => options.UseSqlite(_connection);

    private readonly object _seedLock = new();
    private bool _seeded;

    /// <summary>
    /// Creates the schema and a small desk: two agents (one inactive), a customer and a ticket. Done
    /// once, however many tests ask, because they share this one in-memory database.
    /// </summary>
    public void SeedDesk()
    {
        lock (_seedLock)
        {
            if (_seeded)
            {
                return;
            }

            Seed();
            _seeded = true;
        }
    }

    private void Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var hasher = new IdentityPasswordHasher();

        db.Database.EnsureCreated();

        var category = new Category("Technical", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Fabrikam Inc", "helpdesk@fabrikam.test", null, CustomerTier.Standard, FixedClock.DefaultNow);

        var alex = new Agent("Alex Turner", ActiveEmail, 10, FixedClock.DefaultNow);
        alex.SetPasswordHash(hasher.Hash(Password));

        var yuki = new Agent("Yuki Tanaka", InactiveEmail, 8, FixedClock.DefaultNow);
        yuki.SetPasswordHash(hasher.Hash(Password));
        yuki.Deactivate();

        db.AddRange(category, customer, alex, yuki);
        db.SaveChanges();

        db.Add(Ticket.Raise(
            "TCK-0001", "Cannot log in", "Happens for every user since this morning.",
            customer.Id, category.Id, TicketPriority.Medium, DateTime.UtcNow, DateTime.UtcNow.AddHours(24)));
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
