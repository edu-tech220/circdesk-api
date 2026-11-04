using System.Net.Http.Headers;
using System.Net.Http.Json;
using CircDesk.Api.Auth;
using CircDesk.Api.Data;
using CircDesk.Api.Domain;
using CircDesk.Api.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CircDesk.Api.Tests.Infrastructure;

public static class TestAuth
{
    public const string DefaultPassword = "Password123!";

    public static async Task<(string Token, int MemberId)> RegisterAsync(HttpClient client, string username, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var memberId = ExtractMemberId(body!.Token);
        return (body.Token, memberId);
    }

    public static async Task<(string Token, int MemberId)> CreateLibrarianAsync(
        CircDeskApiFactory factory, HttpClient client, string username, string password = DefaultPassword)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CircDeskDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var (hash, salt) = hasher.Hash(password);

            db.Members.Add(new Member
            {
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = MemberRole.Librarian,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var memberId = ExtractMemberId(body!.Token);
        return (body.Token, memberId);
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static int ExtractMemberId(string jwt)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var idClaim = token.Claims.First(c => c.Type is "nameid" or System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.Parse(idClaim.Value);
    }
}
