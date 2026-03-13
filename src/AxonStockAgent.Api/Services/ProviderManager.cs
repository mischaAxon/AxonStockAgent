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

        // Bouw lokale lijsten op vóór toewijzing aan velden.
        // Zo zien concurrent tasks pas een non-null _marketData als de lijst volledig gevuld is.
        var md = new List<IMarketDataProvider>();
        var ns = new List<INewsProvider>();
        var fp = new List<IFundamentalsProvider>();

        var configs = await _db.DataProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Id)
            .ToListAsync();

        foreach (var config in configs)
        {
            var provider = CreateProvider(config);
            if (provider is null) continue;

            if (provider is IMarketDataProvider  m) md.Add(m);
            if (provider is INewsProvider         n) ns.Add(n);
            if (provider is IFundamentalsProvider f) fp.Add(f);
        }

        _news         = ns;
        _fundamentals = fp;
        _marketData   = md; // stel als laatste in — dit is het signaal dat laden klaar is

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

            "fmp" => new FmpProvider(
                _sp.GetRequiredService<IHttpClientFactory>().CreateClient("fmp"),
                apiKey ?? "",
                _sp.GetRequiredService<ILogger<FmpProvider>>()),

            "twelvedata" => new TwelveDataProvider(
                _sp.GetRequiredService<IHttpClientFactory>().CreateClient("twelvedata"),
                apiKey ?? "",
                _sp.GetRequiredService<ILogger<TwelveDataProvider>>()),

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
    /// Volgorde bepaald door Priority in de database (lager = eerder).
    /// Exchange-match gaat voor prioriteit.
    /// </summary>
    public async Task<IMarketDataProvider?> GetMarketDataProvider(string? preferredExchange = null)
    {
        await EnsureLoaded();

        if (preferredExchange != null)
        {
            var match = _marketData!.FirstOrDefault(p =>
                p.SupportedExchanges.Contains(preferredExchange, StringComparer.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return _marketData!.FirstOrDefault();
    }

    /// <summary>Retourneert alle actieve news providers.</summary>
    public async Task<INewsProvider[]> GetAllNewsProviders()
    {
        await EnsureLoaded();
        return _news!.ToArray();
    }

    /// <summary>
    /// Retourneert de beste fundamentals provider.
    /// Volgorde bepaald door Priority in de database (lager = eerder).
    /// </summary>
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

    /// <summary>
    /// Haal quotes op voor meerdere symbolen via één bulk API call.
    /// Voorkeur: TwelveData (realtime, geen daglimiet op betaalde plannen).
    /// Fallback: EODHD bulk endpoint. Laatste fallback: losse GetQuote calls.
    /// </summary>
    public async Task<Dictionary<string, Quote>> GetBulkQuotes(string[] symbols)
    {
        await EnsureLoaded();

        // Voorkeur: TwelveData (realtime)
        var twelve = _marketData!.OfType<TwelveDataProvider>().FirstOrDefault();
        if (twelve != null)
            return await twelve.GetBulkQuotes(symbols);

        // Fallback: EODHD bulk endpoint
        var eodhd = _marketData!.OfType<EodhdProvider>().FirstOrDefault();
        if (eodhd != null)
            return await eodhd.GetBulkQuotes(symbols);

        // Laatste fallback: losse calls
        var result = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        foreach (var sym in symbols)
        {
            var q = await GetQuote(sym);
            if (q != null) result[sym] = q;
        }
        return result;
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
