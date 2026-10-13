using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Features.Auth.Commands.Login;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// The whole API over real HTTP: who can sign in, what an unsigned-in caller can reach, and what
/// the token does. These are the behaviours the brief asks for, checked as a caller sees them.
/// </summary>
public sealed class ApiAuthenticationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiAuthenticationTests(ApiFactory factory)
    {
        _factory = factory;
        factory.SeedDesk();
        _client = factory.CreateClient();
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<string> SignInAs(string email)
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new { email, password = ApiFactory.Password }));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private HttpRequestMessage Request(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = Json(body);
        }

        return request;
    }

    private static string TokenSignedWith(string key, DateTime expires, DateTime? notBefore = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "SupportDesk.Api",
            Audience = "SupportDesk.Web",
            Subject = new System.Security.Claims.ClaimsIdentity([new("sub", "1"), new("name", "Alex Turner")]),
            NotBefore = notBefore ?? expires.AddHours(-1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });

    // ---- Signing in ----

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsATokenItsExpiryAndWhoSignedIn()
    {
        var response = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.ActiveEmail, password = ApiFactory.Password }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.True(body.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow.AddMinutes(55));
        Assert.Equal("Alex Turner", body.GetProperty("agent").GetProperty("fullName").GetString());
        Assert.Equal(ApiFactory.ActiveEmail, body.GetProperty("agent").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_Is401AndIssuesNoToken()
    {
        var response = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.ActiveEmail, password = "not-the-password" }));

        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_GivesTheSame401WhetherTheEmailOrThePasswordWasWrong()
    {
        var wrongPassword = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.ActiveEmail, password = "wrong" }));
        var unknownEmail = await _client.PostAsync(
            "/api/auth/login", Json(new { email = "nobody@example.test", password = ApiFactory.Password }));
        var inactive = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.InactiveEmail, password = ApiFactory.Password }));

        Assert.All(new[] { wrongPassword, unknownEmail, inactive }, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));

        var details = new List<string?>();

        foreach (var response in new[] { wrongPassword, unknownEmail, inactive })
        {
            details.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        }

        Assert.Single(details.Distinct());
        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, details[0]);
    }

    [Fact]
    public async Task Login_AnInactiveAgentCannotSignInEvenWithTheRightPassword()
    {
        var response = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.InactiveEmail, password = ApiFactory.Password }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "a-password")]
    [InlineData("alex@example.test", "")]
    public async Task Login_WithAMissingEmailOrPassword_Is400(string email, string password)
    {
        var response = await _client.PostAsync("/api/auth/login", Json(new { email, password }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_NeedsNoToken_AndSwaggerIsOpenToo()
    {
        var login = await _client.PostAsync(
            "/api/auth/login", Json(new { email = ApiFactory.ActiveEmail, password = ApiFactory.Password }));
        var swagger = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    // ---- Everything else is closed without a token ----

    public static TheoryData<string, string> EveryProtectedEndpoint => new()
    {
        { "GET", "/api/tickets" },
        { "GET", "/api/tickets/1" },
        { "POST", "/api/tickets" },
        { "PATCH", "/api/tickets/1/status" },
        { "PATCH", "/api/tickets/1/assignment" },
        { "POST", "/api/tickets/1/escalate" },
        { "GET", "/api/tickets/1/escalations" },
        { "GET", "/api/customers" },
        { "GET", "/api/customers/1" },
        { "GET", "/api/agents" },
        { "GET", "/api/categories" },
        { "GET", "/api/auth/me" }
    };

    [Theory]
    [MemberData(nameof(EveryProtectedEndpoint))]
    public async Task AnEndpoint_WithoutAToken_Is401(string method, string url)
    {
        var response = await _client.SendAsync(Request(new HttpMethod(method), url, token: null, body: new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/tickets/1/escalations")]
    [InlineData("/api/customers")]
    [InlineData("/api/customers/1")]
    [InlineData("/api/agents")]
    [InlineData("/api/categories")]
    [InlineData("/api/auth/me")]
    public async Task AnEndpoint_WithAValidToken_Is200(string url)
    {
        var token = await SignInAs(ApiFactory.ActiveEmail);

        var response = await _client.SendAsync(Request(HttpMethod.Get, url, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ARefusedCall_ReturnsAProblemDocumentLikeEveryOtherError()
    {
        var response = await _client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
    }

    // ---- What the token must be to be accepted ----

    [Fact]
    public async Task ATokenSignedWithAnotherKey_Is401()
    {
        var token = TokenSignedWith("a-completely-different-signing-key-32-bytes!", DateTime.UtcNow.AddHours(1));

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/tickets", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiredToken_Is401()
    {
        var token = TokenSignedWith(ApiFactory.SigningKey, DateTime.UtcNow.AddHours(-1));

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/tickets", token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenThatIsNotOne_Is401()
    {
        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/tickets", "not.a.token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenWithTheRightKeyAndAFutureExpiry_IsAccepted()
    {
        var token = TokenSignedWith(ApiFactory.SigningKey, DateTime.UtcNow.AddHours(1));

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/tickets", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- /me ----

    [Fact]
    public async Task Me_ReturnsTheSignedInAgent()
    {
        var token = await SignInAs(ApiFactory.ActiveEmail);

        var response = await _client.SendAsync(Request(HttpMethod.Get, "/api/auth/me", token));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Alex Turner", body.GetProperty("fullName").GetString());
        Assert.Equal(ApiFactory.ActiveEmail, body.GetProperty("email").GetString());
    }

    // ---- Who escalated comes from the token ----

    [Fact]
    public async Task Escalate_RecordsTheSignedInAgent_WhateverTheBodySaysEscalatedByIs()
    {
        var token = await SignInAs(ApiFactory.ActiveEmail);

        var escalate = await _client.SendAsync(Request(
            HttpMethod.Post, "/api/tickets/1/escalate", token,
            new { reason = "Customer called twice and needs an answer.", escalatedBy = "somebody.else" }));

        Assert.Equal(HttpStatusCode.OK, escalate.StatusCode);

        var history = await _client.SendAsync(Request(HttpMethod.Get, "/api/tickets/1/escalations", token));
        var entry = (await history.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().First();

        Assert.Equal("Alex Turner", entry.GetProperty("escalatedBy").GetString());
    }

    [Fact]
    public async Task Escalate_NeedsNoEscalatedByInTheBody()
    {
        var token = await SignInAs(ApiFactory.ActiveEmail);

        var response = await _client.SendAsync(Request(
            HttpMethod.Post, "/api/tickets/1/escalate", token, new { reason = "Customer called twice again." }));

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
