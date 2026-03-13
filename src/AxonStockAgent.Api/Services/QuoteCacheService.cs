using AxonStockAgent.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// In-memory cache voor realtime quotes.
/// Real-time quotes worden 30 seconden gecacht; EOD fallback quotes 15 minuten.
/// </summary>
public class QuoteCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ProviderManager _providers;
    private readonly ILogger<QuoteCacheService> _logger;

    private static readonly TimeSpan CacheDuration    = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EodCacheDuration = TimeSpan.FromMinutes(15);

    public QuoteCacheService(IMemoryCache cache, ProviderManager providers, ILogger<QuoteCacheService> logger)
    {
        _cache     = cache;
        _providers = providers;
        _logger    = logger;
    }

    /// <summary>
    /// Haal een quote op, met cache. Gecachte quotes zijn maximaal 30s (RT) of 15m (EOD) oud.
    /// </summary>
    public async Task<Quote?> GetQuote(string symbol)
    {
        var cacheKey = $"quote:{symbol.ToUpper()}";

        if (_cache.TryGetValue(cacheKey, out Quote? cached))
            return cached;

        var quote = await _providers.GetQuote(symbol);

        if (quote != null)
        {
            // EOD fallback quotes veranderen niet intraday — langer cachen
            var isEodFallback = quote.Volume == 0 || quote.Open == 0;
            var ttl = isEodFallback ? EodCacheDuration : CacheDuration;

            _cache.Set(cacheKey, quote, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            });
        }

        return quote;
    }

    /// <summary>
    /// Haal quotes op voor meerdere symbolen. Gebruikt cache waar mogelijk.
    /// Missers worden opgehaald met max 5 gelijktijdige EODHD calls (rate limit bescherming).
    /// </summary>
    public async Task<Dictionary<string, Quote>> GetBatchQuotes(string[] symbols)
    {
        var results = new Dictionary<string, Quote>();
        var misses  = new List<string>();

        // Check cache eerst
        foreach (var symbol in symbols)
        {
            var cacheKey = $"quote:{symbol.ToUpper()}";
            if (_cache.TryGetValue(cacheKey, out Quote? cached) && cached != null)
                results[symbol] = cached;
            else
                misses.Add(symbol);
        }

        if (misses.Count > 0)
        {
            _logger.LogInformation("QuoteCache batch: {Hits} cache hits, {Misses} misses van {Total} gevraagd",
                results.Count, misses.Count, symbols.Length);

            // Beperk parallelisme: max 5 gelijktijdige EODHD calls
            using var semaphore = new SemaphoreSlim(5, 5);

            var tasks = misses.Select(async symbol =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var quote = await _providers.GetQuote(symbol);
                    return (symbol, quote);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fout bij ophalen quote voor {Symbol}", symbol);
                    return (symbol, (Quote?)null);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var fetchResults = await Task.WhenAll(tasks);

            int fetched = 0, failed = 0;
            foreach (var (symbol, quote) in fetchResults)
            {
                if (quote != null)
                {
                    var cacheKey       = $"quote:{symbol.ToUpper()}";
                    var isEodFallback  = quote.Volume == 0 || quote.Open == 0;
                    var ttl            = isEodFallback ? EodCacheDuration : CacheDuration;

                    _cache.Set(cacheKey, quote, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl,
                        Size = 1
                    });
                    results[symbol] = quote;
                    fetched++;
                }
                else
                {
                    failed++;
                }
            }

            if (failed > 0)
            {
                var stillMissing = misses.Where(m => !results.ContainsKey(m)).Take(10);
                _logger.LogWarning("QuoteCache: {Fetched} opgehaald, {Failed} mislukt. Voorbeelden: [{Examples}]",
                    fetched, failed, string.Join(", ", stillMissing));
            }
        }

        return results;
    }
}
