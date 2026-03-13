using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AxonStockAgent.Api.Services;

namespace AxonStockAgent.Api.BackgroundServices;

public class NewsFetcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NewsFetcherService> _logger;
    private readonly int _intervalSeconds;
    private readonly bool _enabled;

    public NewsFetcherService(IServiceScopeFactory scopeFactory, ILogger<NewsFetcherService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = config.GetValue<int>("News:FetchIntervalSeconds", 60);
        _enabled = config.GetValue<bool>("News:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("News fetcher is disabled via configuration");
            return;
        }

        _logger.LogInformation("News fetcher started, interval: {Interval}s", _intervalSeconds);

        // Wacht 90s na startup zodat de quote batch-calls niet geblokkeerd worden
        // door de EODHD rate limiter (news fetcht 69 symbolen × 650ms = ~45s)
        await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var newsService = scope.ServiceProvider.GetRequiredService<NewsService>();

                _logger.LogDebug("Fetching latest news...");
                await newsService.FetchLatestNews();
                await newsService.CalculateSectorSentiment();
                _logger.LogDebug("News fetch cycle complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in news fetch cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }
}
