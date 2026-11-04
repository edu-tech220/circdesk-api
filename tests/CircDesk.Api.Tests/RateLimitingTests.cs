using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

/// <summary>
/// Uses its own CircDeskApiFactory (not shared with other test classes) so the fixed-window
/// counter starts at zero and isn't polluted by auth calls made in other test suites.
/// </summary>
public class RateLimitingTests : IClassFixture<CircDeskApiFactory>, IAsyncLifetime
{
    private readonly CircDeskApiFactory _factory;
    private HttpClient _client = null!;

    public RateLimitingTests(CircDeskApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Login_ExceedsPermitLimit_ReturnsTooManyRequests()
    {
        HttpStatusCode? rejected = null;

        for (var i = 0; i < 60 && rejected is null; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", "wrong-password"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response.StatusCode;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected);
    }
}
