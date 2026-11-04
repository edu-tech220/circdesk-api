using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_NewUsername_ReturnsTokenWithMemberRole()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("alice", TestAuth.DefaultPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(MemberRole.Member, body.Role);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        await TestAuth.RegisterAsync(Client, "bob");

        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bob", TestAuth.DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await TestAuth.RegisterAsync(Client, "carol");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("carol", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ghost", "whatever123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
