using AxonStockAgent.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// In-memory cache voor realtime quotes.
/// Elke quote wordt 30 seconden gecacht om API rate limits te respecteren.
/// </summary>
public class QuoteCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ProviderManager _providers;
    private readonly ILogger<QuoteCacheService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public QuoteCacheService(IMemoryCache cache, ProviderManager providers, ILogger<QuoteCacheService> logger)
    {
        _cache     = cache;
        _providers = providers;
        _logger    = logger;
    }

    /// <summary>
    /// Haal een quote op, met cache. Gecachte quotes zijn maximaal 30 seconden oud.
    /// </summary>
    public async Task<Quote?> GetQuote(string symbol)
    {
        var cacheKey = $"quote:{symbol.ToUpper()}";

        if (_cache.TryGetValue(cacheKey, out Quote? cached))
            return cached;

        var quote = await _providers.GetQuote(symbol);

        if (quote != null)
        {
            _cache.Set(cacheKey, quote, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });
        }

        return quote;
    }

    /// <summary>
    /// Haal quotes op voor meerdere symbolen. Gebruikt cache waar mogelijk.
    /// Missers worden parallel opgehaald bij de provider.
    /// </summary>
    public async Task<Dictionary<string, Quote>> GetBatchQuotes(string[] symbols)
    {
        var results = new Dictionary<string, Quote>();
        var misses = new List<string>();

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
            _logger.LogDebug("QuoteCache: {Hits} hits, {Misses} misses", results.Count, misses.Count);

            var tasks = misses.Select(async symbol =>
            {
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
            });

            foreach (var (symbol, quote) in await Task.WhenAll(tasks))
            {
                if (quote != null)
                {
                    var cacheKey = $"quote:{symbol.ToUpper()}";
                    _cache.Set(cacheKey, quote, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheDuration,
                        Size = 1
                    });
                    results[symbol] = quote;
                }
            }
        }

        return results;
    }
}
