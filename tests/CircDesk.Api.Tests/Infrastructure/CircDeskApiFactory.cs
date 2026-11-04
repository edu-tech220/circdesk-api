using CircDesk.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CircDesk.Api.Tests.Infrastructure;

/// <summary>
/// Boots the API in-process against an in-memory SQLite database instead of SQL Server, so the
/// whole test suite runs with `dotnet test` and no external services. Each DbContext gets its own
/// connection (as in production) pointed at the same shared-cache named database, rather than all
/// sharing one SqliteConnection object — a single connection isn't safe for the genuinely
/// concurrent requests the concurrency tests fire, and serializes away the race they exist to catch.
/// </summary>
public sealed class CircDeskApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = $"circdesk-tests-{Guid.NewGuid():N}",
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    private readonly SqliteConnection _keepAliveConnection;

    public CircDeskApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "CircDeskTests",
                ["Jwt:Audience"] = "CircDeskTests.Clients",
                ["Jwt:Key"] = "TEST_ONLY_SIGNING_KEY_AT_LEAST_32_CHARACTERS_LONG",
                ["Jwt:ExpiresMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CircDeskDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<CircDeskDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    /// <summary>
    /// Clears every table so each test method starts from a clean database. Deletes rows instead
    /// of EnsureDeleted+EnsureCreate: dropping/recreating the schema on a shared-cache SQLite
    /// database while the keep-alive connection is open is unreliable.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CircDeskDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Child-to-parent order to satisfy foreign key constraints.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Fines");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Holds");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Loans");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Copies");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Books");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Members");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
