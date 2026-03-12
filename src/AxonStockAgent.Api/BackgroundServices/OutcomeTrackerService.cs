using AxonStockAgent.Api.Services;

namespace AxonStockAgent.Api.BackgroundServices;

/// <summary>
/// Achtergrond-job die elke 6 uur signaal-outcomes bijwerkt.
/// Draait in de API-container omdat het lichtgewicht is
/// en alleen bestaande signalen + candle data nodig heeft.
/// </summary>
public class OutcomeTrackerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutcomeTrackerService> _logger;

    private static readonly TimeSpan Interval     = TimeSpan.FromHours(6);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    public OutcomeTrackerService(IServiceScopeFactory scopeFactory, ILogger<OutcomeTrackerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outcome tracker gestart, interval: {Interval}", Interval);

        // Wacht even tot de database en providers klaar zijn
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var outcomeService = scope.ServiceProvider.GetRequiredService<SignalOutcomeService>();

                var updated = await outcomeService.ProcessOutcomesAsync(stoppingToken);
                if (updated > 0)
                    _logger.LogInformation("Outcome tracker: {Count} signalen bijgewerkt", updated);
                else
                    _logger.LogDebug("Outcome tracker: geen signalen om bij te werken");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outcome tracker cycle mislukt");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
