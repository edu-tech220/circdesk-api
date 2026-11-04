using CircDesk.Api.Services;

namespace CircDesk.Api.BackgroundServices;

/// <summary>
/// Periodically expires Ready holds whose pickup window has passed and cascades the copy to the
/// next member in the queue (or back to Available if the queue is empty).
/// </summary>
public class HoldExpirationService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<HoldExpirationService> _logger;

    public HoldExpirationService(IServiceScopeFactory scopeFactory, IHostEnvironment environment, ILogger<HoldExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Integration tests drive hold expiration explicitly via IHoldService and reset the
        // database between tests; a background poller running concurrently against the same
        // SQLite connection would race those resets.
        if (_environment.IsEnvironment("Testing"))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var holdService = scope.ServiceProvider.GetRequiredService<IHoldService>();
                var expiredCount = await holdService.ExpireDueHoldsAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    _logger.LogInformation("Se expiraron {Count} reservas vencidas.", expiredCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error al expirar reservas vencidas.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
