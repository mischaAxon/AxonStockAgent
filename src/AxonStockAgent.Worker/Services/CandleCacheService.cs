using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AxonStockAgent.Worker.Services;

/// <summary>
/// Cacht candle data in-memory om onnodige API-calls te voorkomen.
/// TTL is afhankelijk van de scan-modus:
/// - EOD mode: 24 uur (data verandert niet tot volgende close)
/// - Realtime mode: 5 minuten (laatste bar kan veranderen)
/// </summary>
public class CandleCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CandleCacheService> _logger;

    // Default TTLs — worden overschreven door SetMode()
    private TimeSpan _ttl = TimeSpan.FromHours(24);
    private bool _realtimeMode;

    public CandleCacheService(IMemoryCache cache, ILogger<CandleCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Stel de cache-modus in op basis van de scan-modus.
    /// Roep dit aan bij het begin van elke scan cycle.
    /// </summary>
    public void SetMode(bool realtimeMode)
    {
        _realtimeMode = realtimeMode;
        _ttl = realtimeMode ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Haal candles op uit cache of via de provider.
    /// Cache key: "candles:{symbol}:{resolution}:{count}"
    /// </summary>
    public async Task<Candle[]?> GetCandlesAsync(
        IMarketDataProvider provider,
        string symbol,
        string resolution,
        int count)
    {
        var cacheKey = $"candles:{symbol}:{resolution}:{count}";

        if (_cache.TryGetValue(cacheKey, out Candle[]? cached) && cached != null)
        {
            _logger.LogDebug("Cache HIT voor {Symbol} candles ({Count} bars, TTL={Ttl})",
                symbol, cached.Length, _ttl);
            return cached;
        }

        _logger.LogDebug("Cache MISS voor {Symbol} candles — fetching van provider", symbol);

        var candles = await provider.GetCandles(symbol, resolution, count);

        if (candles != null && candles.Length > 0)
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_ttl)
                .SetSize(candles.Length); // voor cache size limiting

            _cache.Set(cacheKey, candles, options);
            _logger.LogDebug("Cache SET voor {Symbol}: {Count} candles, TTL={Ttl}", symbol, candles.Length, _ttl);
        }

        return candles;
    }

    /// <summary>
    /// Verwijder cached candles voor een specifiek symbool.
    /// Nuttig als je weet dat de data verouderd is (bv. na marktuur-overgang).
    /// </summary>
    public void Invalidate(string symbol, string resolution, int count)
    {
        var cacheKey = $"candles:{symbol}:{resolution}:{count}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Cache INVALIDATED voor {Symbol}", symbol);
    }

    /// <summary>
    /// Geeft cache-statistieken terug voor logging.
    /// </summary>
    public (bool RealtimeMode, TimeSpan Ttl) GetStatus() => (_realtimeMode, _ttl);
}
