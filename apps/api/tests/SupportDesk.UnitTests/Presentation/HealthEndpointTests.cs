using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// <c>GET /health</c> as a load balancer or a container orchestrator calls it: no token, and the
/// status code is the answer. A 200 means the API can reach its database, a 503 means it cannot.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        factory.SeedDesk();
    }

    [Fact]
    public async Task Is_open_and_healthy_when_the_database_can_be_reached()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Does_not_need_a_token_even_when_a_wrong_one_is_sent()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Leaves_the_rest_of_the_api_closed()
    {
        var response = await _factory.CreateClient().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reports_unavailable_when_the_database_cannot_be_reached()
    {
        await using var factory = new UnreachableDatabaseFactory();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Does_not_leak_why_the_database_is_unreachable()
    {
        await using var factory = new UnreachableDatabaseFactory();

        var text = await (await factory.CreateClient().GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("nowhere", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The API with a database file that does not exist and cannot be created.</summary>
    private sealed class UnreachableDatabaseFactory : ApiFactory
    {
        protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
            options.UseSqlite("Data Source=nowhere/support.db;Mode=ReadOnly");
    }
}
