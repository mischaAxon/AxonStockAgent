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
    /// Cache-misses worden opgehaald via EODHD bulk endpoint (1 HTTP call voor alle misses).
    /// Symbolen die de bulk niet retourneert (EOD-only of delisted) krijgen individuele EOD fallback.
    /// </summary>
    public async Task<Dictionary<string, Quote>> GetBatchQuotes(string[] symbols)
    {
        var results = new Dictionary<string, Quote>();
        var misses  = new List<string>();

        // ── 1. Check cache ──
        foreach (var symbol in symbols)
        {
            var cacheKey = $"quote:{symbol.ToUpper()}";
            if (_cache.TryGetValue(cacheKey, out Quote? cached) && cached != null)
                results[symbol] = cached;
            else
                misses.Add(symbol);
        }

        if (misses.Count == 0) return results;

        _logger.LogInformation("QuoteCache batch: {Hits} cache hits, {Misses} misses van {Total}",
            results.Count, misses.Count, symbols.Length);

        // ── 2. Bulk fetch via EODHD bulk endpoint (1 HTTP call!) ──
        var bulkResults = await _providers.GetBulkQuotes(misses.ToArray());

        foreach (var (symbol, quote) in bulkResults)
        {
            CacheAndStore(results, symbol, quote, isEodFallback: false);
        }

        // ── 3. EOD fallback voor symbolen die de bulk niet retourneerde ──
        var stillMissing = misses.Where(m => !results.ContainsKey(m)).ToList();

        if (stillMissing.Count > 0)
        {
            _logger.LogDebug("QuoteCache: {Count} symbolen niet in bulk, probeer EOD fallback: [{Syms}]",
                stillMissing.Count, string.Join(", ", stillMissing));

            foreach (var symbol in stillMissing)
            {
                try
                {
                    var quote = await _providers.GetQuote(symbol);
                    if (quote != null)
                        CacheAndStore(results, symbol, quote, isEodFallback: true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("EOD fallback mislukt voor {Symbol}: {Msg}", symbol, ex.Message);
                }
            }
        }

        var totalFailed = symbols.Length - results.Count;
        if (totalFailed > 0)
        {
            var failedSyms = symbols.Where(s => !results.ContainsKey(s)).Take(10);
            _logger.LogWarning("QuoteCache: {Failed} symbolen zonder quote: [{Syms}]",
                totalFailed, string.Join(", ", failedSyms));
        }

        return results;
    }

    private void CacheAndStore(Dictionary<string, Quote> results, string symbol, Quote quote, bool isEodFallback)
    {
        var cacheKey = $"quote:{symbol.ToUpper()}";
        var ttl      = isEodFallback ? EodCacheDuration : CacheDuration;

        _cache.Set(cacheKey, quote, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1
        });
        results[symbol] = quote;
    }
}
