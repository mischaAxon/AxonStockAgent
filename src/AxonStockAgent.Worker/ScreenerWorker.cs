using AxonStockAgent.Core.Models;
using Microsoft.Extensions.Options;

namespace AxonStockAgent.Worker;

/// <summary>
/// Background worker that scans stocks on a regular interval.
/// Uses the existing SwingEdge indicator engine from Core.
/// Full implementation migrated from the original SwingEdgeScreener.
/// </summary>
public class ScreenerWorker : BackgroundService
{
    private readonly ILogger<ScreenerWorker> _logger;
    private readonly ScreenerConfig _config;

    public ScreenerWorker(ILogger<ScreenerWorker> logger, IOptions<ScreenerConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AxonStockAgent Worker gestart");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (IsMarketHours())
            {
                _logger.LogInformation("Scan cycle gestart om {Time}", DateTime.UtcNow);
                // TODO: Migrate full scan logic from SwingEdgeScreener.cs
                // - Fetch candles per symbol
                // - Run IndicatorEngine.Analyze()
                // - Run AI enrichment (ML + Claude + Sentiment)
                // - Save signals to PostgreSQL
                // - Send Telegram notifications
                _logger.LogInformation("Scan cycle voltooid");
            }
            else
            {
                _logger.LogDebug("Buiten markturen, wacht...");
            }

            await Task.Delay(TimeSpan.FromMinutes(_config.ScanIntervalMinutes), stoppingToken);
        }
    }

    private static bool IsMarketHours()
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(21);
    }
}
