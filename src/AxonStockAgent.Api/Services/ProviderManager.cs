using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Providers;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Laadt actieve provider-configuraties uit de database en biedt
/// factory-methoden om de juiste provider instantie op te halen.
/// Geregistreerd als Scoped service.
/// </summary>
public class ProviderManager
{
    private readonly AppDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly ILogger<ProviderManager> _logger;

    private List<IMarketDataProvider>?  _marketData;
    private List<INewsProvider>?        _news;
    private List<IFundamentalsProvider>? _fundamentals;

    public ProviderManager(AppDbContext db, IServiceProvider sp, ILogger<ProviderManager> logger)
    {
        _db     = db;
        _sp     = sp;
        _logger = logger;
    }

    // ── Lazy loader ────────────────────────────────────────────────────────────

    private async Task EnsureLoaded()
    {
        if (_marketData != null) return;

        _marketData   = new();
        _news         = new();
        _fundamentals = new();

        var configs = await _db.DataProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Id)
            .ToListAsync();

        foreach (var config in configs)
        {
            var provider = CreateProvider(config);
            if (provider is null) continue;

            if (provider is IMarketDataProvider  md) _marketData.Add(md);
            if (provider is INewsProvider         np) _news.Add(np);
            if (provider is IFundamentalsProvider fp) _fundamentals.Add(fp);
        }

        _logger.LogInformation(
            "ProviderManager: {MD} market-data, {N} news, {F} fundamentals providers geladen",
            _marketData.Count, _news.Count, _fundamentals.Count);
    }

    // ── Factory ────────────────────────────────────────────────────────────────

    private object? CreateProvider(Data.Entities.DataProviderEntity config)
    {
        var apiKey = DecryptApiKey(config.ApiKeyEncrypted);

        return config.Name switch
        {
            "finnhub" => new FinnhubProvider(
                _sp.GetRequiredService<IHttpClientFactory>().CreateClient("finnhub"),
                apiKey ?? "",
                _sp.GetRequiredService<ILogger<FinnhubProvider>>()),

            "eodhd" => new EodhdProvider(
                _sp.GetRequiredService<IHttpClientFactory>().CreateClient("eodhd"),
                apiKey ?? "",
                _sp.GetRequiredService<ILogger<EodhdProvider>>()),

            _ => LogUnknown(config.Name)
        };
    }

    private object? LogUnknown(string name)
    {
        _logger.LogWarning("Geen implementatie gevonden voor provider '{Name}'", name);
        return null;
    }

    /// <summary>Eenvoudige passthrough — vervang door echte encryptie in productie.</summary>
    private static string? DecryptApiKey(string? encrypted) => encrypted;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Geeft de beste market-data provider voor het opgegeven symbool.
    /// Prioriteit: exchange-match → EODHD (betrouwbare candles) → eerste beschikbare.
    /// </summary>
    public async Task<IMarketDataProvider?> GetMarketDataProvider(string? preferredExchange = null)
    {
        await EnsureLoaded();

        // 1. Probeer exchange-specifieke match
        if (preferredExchange != null)
        {
            var match = _marketData!.FirstOrDefault(p =>
                p.SupportedExchanges.Contains(preferredExchange, StringComparer.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 2. Geef voorkeur aan EODHD — Finnhub blokkeert /stock/candle op gratis tier
        var eodhd = _marketData!.OfType<EodhdProvider>().FirstOrDefault();
        if (eodhd != null) return eodhd;

        return _marketData!.FirstOrDefault();
    }

    /// <summary>Retourneert alle actieve news providers.</summary>
    public async Task<INewsProvider[]> GetAllNewsProviders()
    {
        await EnsureLoaded();
        return _news!.ToArray();
    }

    /// <summary>Retourneert de eerste actieve fundamentals provider.</summary>
    public async Task<IFundamentalsProvider?> GetFundamentalsProvider()
    {
        await EnsureLoaded();
        return _fundamentals!.FirstOrDefault();
    }

    /// <summary>Retourneert een specifieke provider op naam als die actief is.</summary>
    public async Task<object?> GetProviderByName(string name)
    {
        await EnsureLoaded();
        return (object?)_marketData!.FirstOrDefault(p => p.Name == name)
            ?? (object?)_news!.FirstOrDefault(p => p.Name == name)
            ?? (object?)_fundamentals!.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>Haal een realtime/delayed quote op voor een symbool.</summary>
    public async Task<Quote?> GetQuote(string symbol)
    {
        var provider = await GetMarketDataProvider();
        if (provider == null) return null;
        return await provider.GetQuote(symbol);
    }

    /// <summary>Zoek symbolen op ticker of bedrijfsnaam via actieve providers.</summary>
    public async Task<SymbolSearchResult[]> SearchSymbols(string query)
    {
        await EnsureLoaded();

        // Probeer Finnhub eerst (snelste zoekfunctie)
        var finnhub = _marketData!.OfType<FinnhubProvider>().FirstOrDefault()
                   ?? _news!.OfType<FinnhubProvider>().FirstOrDefault()  as FinnhubProvider;
        if (finnhub != null)
        {
            var results = await finnhub.SearchSymbols(query);
            if (results.Length > 0) return results;
        }

        // Fallback: filter op eigen watchlist-symbolen
        var watchlist = await _db.Watchlist
            .Where(w => w.IsActive && (
                w.Symbol.Contains(query.ToUpper()) ||
                (w.Name != null && w.Name.ToLower().Contains(query.ToLower()))))
            .Select(w => new SymbolSearchResult(w.Symbol, w.Name ?? w.Symbol, w.Exchange ?? "", ""))
            .Take(10)
            .ToArrayAsync();

        return watchlist;
    }
}

public record SymbolSearchResult(string Symbol, string Description, string Exchange, string Type);
