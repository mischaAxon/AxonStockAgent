using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/diagnostics")]
[Authorize(Roles = "admin")]
public class DiagnosticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly QuoteCacheService _quoteCache;

    public DiagnosticsController(AppDbContext db, ProviderManager providers, QuoteCacheService quoteCache)
    {
        _db = db;
        _providers = providers;
        _quoteCache = quoteCache;
    }

    /// <summary>
    /// Claude API success rate en faalredenen over de afgelopen N dagen.
    /// </summary>
    [HttpGet("claude/stats")]
    public async Task<IActionResult> GetClaudeStats([FromQuery] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var logs = await _db.ClaudeApiLogs
            .Where(l => l.CreatedAt >= since)
            .ToListAsync();

        if (logs.Count == 0)
            return Ok(new { message = "Geen Claude API logs gevonden", days });

        var totalCalls = logs.Count;
        var successCount = logs.Count(l => l.Status == "success");
        var successRate = (double)successCount / totalCalls;

        var byStatus = logs
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Pct = (double)g.Count() / totalCalls })
            .OrderByDescending(x => x.Count)
            .ToList();

        var bySymbol = logs
            .GroupBy(l => l.Symbol)
            .Select(g => new
            {
                Symbol = g.Key,
                Total = g.Count(),
                Success = g.Count(l => l.Status == "success"),
                SuccessRate = (double)g.Count(l => l.Status == "success") / g.Count(),
                AvgDurationMs = g.Average(l => l.DurationMs)
            })
            .OrderBy(x => x.SuccessRate)
            .ToList();

        var avgDuration = logs.Average(l => l.DurationMs);

        var recentErrors = logs
            .Where(l => l.Status != "success")
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .Select(l => new
            {
                l.Symbol,
                l.Status,
                l.ErrorMessage,
                l.RawResponseSnippet,
                l.HttpStatusCode,
                l.CreatedAt,
                l.DurationMs
            })
            .ToList();

        return Ok(new
        {
            period = new { days, since, until = DateTime.UtcNow },
            summary = new
            {
                totalCalls,
                successCount,
                failureCount = totalCalls - successCount,
                successRate = Math.Round(successRate, 4),
                avgDurationMs = Math.Round(avgDuration, 0)
            },
            byStatus,
            bySymbol,
            recentErrors
        });
    }

    /// <summary>
    /// Overzicht van de data-gezondheid: hoeveel symbolen, quotes, signalen, etc.
    /// </summary>
    [HttpGet("data-health")]
    public async Task<IActionResult> GetDataHealth()
    {
        var totalSymbols = await _db.MarketSymbols.CountAsync(m => m.IsActive);
        var totalIndices = await _db.MarketIndices.CountAsync(i => i.IsEnabled);
        var totalMemberships = await _db.IndexMemberships.CountAsync();

        var totalSignals = await _db.Signals.CountAsync();
        var recentSignals = await _db.Signals.CountAsync(s => s.CreatedAt >= DateTime.UtcNow.AddDays(-7));
        var symbolsWithSignals = await _db.Signals
            .Where(s => s.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .Select(s => s.Symbol)
            .Distinct()
            .CountAsync();

        var totalNews = await _db.NewsArticles.CountAsync();
        var recentNews = await _db.NewsArticles.CountAsync(n => n.PublishedAt >= DateTime.UtcNow.AddDays(-7));
        var totalFundamentals = await _db.CompanyFundamentals.CountAsync();
        var totalWatchlist = await _db.Watchlist.CountAsync(w => w.IsActive);

        var providers = await _db.DataProviders.ToListAsync();
        var providerStatus = providers.Select(p => new
        {
            p.Name,
            p.IsEnabled,
            HasApiKey = !string.IsNullOrEmpty(p.ApiKeyEncrypted),
            p.HealthStatus,
            p.LastHealthCheck
        }).ToList();

        var sampleSymbols = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .OrderBy(m => m.Symbol)
            .Take(10)
            .Select(m => new { m.Symbol, m.Exchange, m.Country, m.Name })
            .ToListAsync();

        var indexDetails = await _db.MarketIndices
            .Where(i => i.IsEnabled)
            .Select(i => new
            {
                i.DisplayName,
                i.IndexSymbol,
                i.ExchangeCode,
                i.SymbolCount,
                i.LastImportAt,
                ActualMemberCount = _db.IndexMemberships.Count(m => m.MarketIndexId == i.Id)
            })
            .ToListAsync();

        return Ok(new
        {
            timestamp = DateTime.UtcNow,
            symbols = new { total = totalSymbols, sample = sampleSymbols },
            indices = new { total = totalIndices, totalMemberships, details = indexDetails },
            signals = new { total = totalSignals, last7Days = recentSignals, symbolsWithSignals },
            news = new { total = totalNews, last7Days = recentNews },
            fundamentals = new { total = totalFundamentals },
            watchlist = new { active = totalWatchlist },
            providers = providerStatus
        });
    }

    /// <summary>
    /// Test quote ophalen voor een specifiek symbool.
    /// </summary>
    [HttpGet("quote-test/{symbol}")]
    public async Task<IActionResult> TestQuote(string symbol)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var provider = await _providers.GetMarketDataProvider();
            if (provider == null)
                return Ok(new { symbol, error = "Geen actieve market data provider gevonden" });

            var quote = await provider.GetQuote(symbol);
            sw.Stop();

            if (quote == null)
                return Ok(new { symbol, provider = provider.Name, error = "Provider retourneerde null", durationMs = sw.ElapsedMilliseconds });

            return Ok(new
            {
                symbol,
                provider = provider.Name,
                durationMs = sw.ElapsedMilliseconds,
                quote = new
                {
                    quote.Symbol,
                    quote.CurrentPrice,
                    quote.PreviousClose,
                    quote.Change,
                    quote.ChangePercent,
                    quote.Open,
                    quote.High,
                    quote.Low,
                    quote.Volume,
                    quote.Timestamp
                }
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new { symbol, error = ex.Message, exceptionType = ex.GetType().Name, durationMs = sw.ElapsedMilliseconds });
        }
    }

    /// <summary>
    /// Test batch quotes voor de eerste N actieve symbolen.
    /// </summary>
    [HttpGet("quote-batch-test")]
    public async Task<IActionResult> TestBatchQuotes([FromQuery] int count = 10)
    {
        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .OrderBy(m => m.Symbol)
            .Take(count)
            .Select(m => m.Symbol)
            .ToArrayAsync();

        var results = new List<object>();
        var provider = await _providers.GetMarketDataProvider();
        var providerName = provider?.Name ?? "none";

        foreach (var symbol in symbols)
        {
            try
            {
                var quote = await _quoteCache.GetQuote(symbol);
                results.Add(new
                {
                    symbol,
                    success = quote != null,
                    price = quote?.CurrentPrice,
                    change = quote?.ChangePercent
                });
            }
            catch (Exception ex)
            {
                results.Add(new { symbol, success = false, error = ex.Message });
            }
        }

        var successCount = results.Count(r => (bool)((dynamic)r).success);

        return Ok(new
        {
            provider = providerName,
            tested = symbols.Length,
            success = successCount,
            failed = symbols.Length - successCount,
            successRate = symbols.Length > 0 ? (double)successCount / symbols.Length : 0,
            results
        });
    }

    /// <summary>
    /// Test quotes voor alle actieve symbolen en retourneer welke falen.
    /// Gebruikt de QuoteCacheService zodat gecachde resultaten niet opnieuw worden opgehaald.
    /// Gebruik ?indexOnly=true (default) om alleen index-leden te testen.
    /// </summary>
    [HttpGet("quote-failures")]
    public async Task<IActionResult> GetQuoteFailures([FromQuery] bool indexOnly = true)
    {
        List<string> symbols;

        if (indexOnly)
        {
            symbols = await _db.IndexMemberships
                .Select(m => m.Symbol)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }
        else
        {
            symbols = await _db.MarketSymbols
                .Where(m => m.IsActive)
                .Select(m => m.Symbol)
                .OrderBy(s => s)
                .ToListAsync();
        }

        var successes = new List<object>();
        var failures  = new List<object>();

        foreach (var symbol in symbols)
        {
            try
            {
                var quote = await _quoteCache.GetQuote(symbol);
                if (quote != null && quote.CurrentPrice > 0)
                    successes.Add(new { symbol, price = quote.CurrentPrice });
                else
                    failures.Add(new { symbol, reason = "null or zero price" });
            }
            catch (Exception ex)
            {
                failures.Add(new { symbol, reason = ex.Message });
            }
        }

        return Ok(new
        {
            tested        = symbols.Count,
            successCount  = successes.Count,
            failureCount  = failures.Count,
            indexOnly,
            failures,
            sampleSuccesses = successes.Take(10)
        });
    }
}
