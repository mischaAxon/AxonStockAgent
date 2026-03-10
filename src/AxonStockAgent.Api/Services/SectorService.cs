using AxonStockAgent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public record SectorSummaryItem(string Sector, int Count);

/// <summary>
/// Verrijkt watchlist-items met sector/industry/land informatie
/// via de actieve fundamentals provider.
/// </summary>
public class SectorService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<SectorService> _logger;

    public SectorService(AppDbContext db, ProviderManager providers, ILogger<SectorService> logger)
    {
        _db        = db;
        _providers = providers;
        _logger    = logger;
    }

    /// <summary>
    /// Haalt company profile op en slaat sector/industry/land op voor het gegeven symbool.
    /// </summary>
    public async Task<bool> EnrichSymbol(string symbol)
    {
        var item = await _db.Watchlist
            .FirstOrDefaultAsync(w => w.Symbol == symbol && w.IsActive);
        if (item == null) return false;

        var provider = await _providers.GetFundamentalsProvider();
        if (provider == null)
        {
            _logger.LogWarning("Geen fundamentals provider beschikbaar voor {Symbol}", symbol);
            return false;
        }

        try
        {
            var profile = await provider.GetProfile(symbol);
            if (profile == null) return false;

            if (!string.IsNullOrEmpty(profile.Sector))   item.Sector    = profile.Sector;
            if (!string.IsNullOrEmpty(profile.Industry)) item.Industry  = profile.Industry;
            if (!string.IsNullOrEmpty(profile.Country))  item.Country   = profile.Country;
            if (profile.MarketCap > 0)                   item.MarketCap = (long)(profile.MarketCap * 1_000_000);
            if (!string.IsNullOrEmpty(profile.Logo))     item.Logo      = profile.Logo;
            if (!string.IsNullOrEmpty(profile.WebUrl))   item.WebUrl    = profile.WebUrl;

            item.SectorSource = provider.Name;
            item.UpdatedAt    = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Symbol {Symbol} verrijkt: {Sector} / {Industry} ({Country})",
                symbol, item.Sector, item.Industry, item.Country);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Verrijking mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Verrijkt alle actieve watchlist-items zonder sector info.
    /// Wacht 1.5 seconde tussen calls (rate limiting).
    /// </summary>
    public async Task<int> EnrichAllWatchlist()
    {
        var items = await _db.Watchlist
            .Where(w => w.IsActive && (w.Sector == null || w.SectorSource == null))
            .ToListAsync();

        int enriched = 0;
        foreach (var item in items)
        {
            var ok = await EnrichSymbol(item.Symbol);
            if (ok) enriched++;
            await Task.Delay(1500);
        }
        return enriched;
    }

    /// <summary>Geeft alle unieke sectoren met het aantal aandelen per sector.</summary>
    public async Task<SectorSummaryItem[]> GetSectorSummary()
    {
        return await _db.Watchlist
            .Where(w => w.IsActive && w.Sector != null)
            .GroupBy(w => w.Sector!)
            .Select(g => new SectorSummaryItem(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToArrayAsync();
    }
}
