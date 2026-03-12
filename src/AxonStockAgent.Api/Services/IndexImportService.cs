using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Providers;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class IndexImportService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ClaudeIndexService _claudeService;
    private readonly ILogger<IndexImportService> _logger;

    // Mapping van onze index-symbolen naar Finnhub-symbolen
    private static readonly Dictionary<string, string> FinnhubIndexMap = new()
    {
        ["GSPC.INDX"]  = "^GSPC",   // S&P 500
        ["NDX.INDX"]   = "^NDX",    // NASDAQ-100
        ["DJI.INDX"]   = "^DJI",    // Dow Jones 30
    };

    public IndexImportService(
        AppDbContext db,
        ProviderManager providers,
        ClaudeIndexService claudeService,
        ILogger<IndexImportService> logger)
    {
        _db            = db;
        _providers     = providers;
        _claudeService = claudeService;
        _logger        = logger;
    }

    /// <summary>
    /// Import via API: probeert Finnhub (voor US) of EODHD (als beschikbaar).
    /// </summary>
    public async Task<(int count, string source)> ImportViaApi(int indexId)
    {
        var index = await _db.MarketIndices.FindAsync(indexId);
        if (index == null) return (0, "not_found");

        // 1. Probeer Finnhub (gratis, voor US-indexen)
        if (FinnhubIndexMap.TryGetValue(index.IndexSymbol, out var finnhubSymbol))
        {
            var providerObj = await _providers.GetProviderByName("finnhub");
            if (providerObj is FinnhubProvider finnhub)
            {
                var tickers = await finnhub.GetIndexConstituents(finnhubSymbol);
                if (tickers.Length > 0)
                {
                    var count = await UpsertMemberships(index, tickers.Select(t => new ComponentInfo(t, "", "")).ToArray());
                    return (count, "finnhub");
                }
            }
        }

        // 2. Fallback: probeer EODHD fundamentals
        var eodhdObj = await _providers.GetProviderByName("eodhd");
        if (eodhdObj is EodhdProvider eodhd)
        {
            var components = await eodhd.GetIndexComponents(index.IndexSymbol);
            if (components.Length > 0)
            {
                var count = await UpsertMemberships(index, components.Select(c => new ComponentInfo(c.Code, c.Name, c.Sector)).ToArray());
                return (count, "eodhd");
            }
        }

        return (0, "no_data");
    }

    /// <summary>
    /// Import via Claude AI: vraag Claude om de index-samenstelling.
    /// Werkt voor alle indexen, maar vooral bedoeld voor NL (AEX, AMX, AScX).
    /// </summary>
    public async Task<(int count, string source)> ImportViaClaude(int indexId)
    {
        var index = await _db.MarketIndices.FindAsync(indexId);
        if (index == null) return (0, "not_found");

        var components = await _claudeService.GetIndexComponentsViaAI(index.DisplayName, index.ExchangeCode);
        if (components.Length == 0) return (0, "claude_empty");

        var count = await UpsertMemberships(index, components.Select(c => new ComponentInfo(c.Code, c.Name, c.Sector)).ToArray());
        return (count, "claude");
    }

    /// <summary>
    /// Bestaande methode - backward compatible wrapper.
    /// </summary>
    public async Task<int> ImportIndexComponents(int indexId)
    {
        var (count, _) = await ImportViaApi(indexId);
        return count;
    }

    // ── Shared upsert logic ──────────────────────────────────────────────

    private record ComponentInfo(string Code, string Name, string Sector);

    private async Task<int> UpsertMemberships(MarketIndexEntity index, ComponentInfo[] components)
    {
        var now = DateTime.UtcNow;
        var exchange = index.ExchangeCode;

        // Verwijder bestaande memberships
        var existing = await _db.IndexMemberships
            .Where(m => m.MarketIndexId == index.Id)
            .ToListAsync();
        _db.IndexMemberships.RemoveRange(existing);

        foreach (var c in components)
        {
            // Bouw volledig symbool: voor US-indexen via Finnhub is code al zonder suffix
            var fullSymbol = c.Code.Contains('.') ? c.Code : $"{c.Code}.{exchange}";

            _db.IndexMemberships.Add(new IndexMembershipEntity
            {
                MarketIndexId = index.Id,
                Symbol        = fullSymbol,
                Name          = c.Name,
                Sector        = c.Sector,
                AddedAt       = now,
            });

            // Zorg dat symbool ook in MarketSymbols staat
            var existsInMarket = await _db.MarketSymbols.AnyAsync(m => m.Symbol == fullSymbol);
            if (!existsInMarket)
            {
                _db.MarketSymbols.Add(new MarketSymbolEntity
                {
                    Symbol     = fullSymbol,
                    Exchange   = exchange,
                    Name       = c.Name,
                    Sector     = c.Sector,
                    Country    = index.Country,
                    IsActive   = true,
                    ImportedAt = now,
                    UpdatedAt  = now,
                });
            }
        }

        index.SymbolCount = components.Length;
        index.LastImportAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Index {Index}: {Count} componenten geïmporteerd", index.DisplayName, components.Length);
        return components.Length;
    }
}
