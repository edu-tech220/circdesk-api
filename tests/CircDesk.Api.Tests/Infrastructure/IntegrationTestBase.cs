namespace CircDesk.Api.Tests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<CircDeskApiFactory>, IAsyncLifetime
{
    protected readonly CircDeskApiFactory Factory;
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(CircDeskApiFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        Client = Factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
