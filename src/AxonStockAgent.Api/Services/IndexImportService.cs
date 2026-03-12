using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Providers;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class IndexImportService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<IndexImportService> _logger;

    public IndexImportService(AppDbContext db, ProviderManager providers, ILogger<IndexImportService> logger)
    {
        _db        = db;
        _providers = providers;
        _logger    = logger;
    }

    /// <summary>
    /// Importeert alle componenten van een index vanuit EODHD.
    /// Upsert: bestaande memberships worden verwijderd en opnieuw aangemaakt.
    /// Zorgt er ook voor dat de symbolen bestaan in MarketSymbols.
    /// </summary>
    public async Task<int> ImportIndexComponents(int indexId)
    {
        var index = await _db.MarketIndices.FindAsync(indexId);
        if (index == null) return 0;

        var providerObj = await _providers.GetProviderByName("eodhd");
        if (providerObj is not EodhdProvider eodhd)
        {
            _logger.LogWarning("EODHD provider niet beschikbaar voor index import");
            return 0;
        }

        var components = await eodhd.GetIndexComponents(index.IndexSymbol);
        if (components.Length == 0)
        {
            _logger.LogWarning("Geen componenten gevonden voor index {Index}", index.IndexSymbol);
            return 0;
        }

        _logger.LogInformation("Importeer {Count} componenten voor index {Index}", components.Length, index.DisplayName);

        var now = DateTime.UtcNow;

        // Verwijder bestaande memberships voor deze index
        var existingMemberships = await _db.IndexMemberships
            .Where(m => m.MarketIndexId == indexId)
            .ToListAsync();
        _db.IndexMemberships.RemoveRange(existingMemberships);

        // Voeg nieuwe memberships toe
        foreach (var c in components)
        {
            // Bepaal het volledige symbool: Code.Exchange (bijv. ASML.AS)
            var exchange = !string.IsNullOrEmpty(c.Exchange) ? c.Exchange : index.ExchangeCode;
            var fullSymbol = $"{c.Code}.{exchange}";

            _db.IndexMemberships.Add(new IndexMembershipEntity
            {
                MarketIndexId = indexId,
                Symbol        = fullSymbol,
                Name          = c.Name,
                Sector        = c.Sector,
                Industry      = c.Industry,
                AddedAt       = now,
            });

            // Zorg dat het symbool ook bestaat in MarketSymbols
            var existsInMarket = await _db.MarketSymbols
                .AnyAsync(m => m.Symbol == fullSymbol);
            if (!existsInMarket)
            {
                _db.MarketSymbols.Add(new MarketSymbolEntity
                {
                    Symbol     = fullSymbol,
                    Exchange   = exchange,
                    Name       = c.Name,
                    Sector     = c.Sector,
                    Industry   = c.Industry,
                    Country    = index.Country,
                    IsActive   = true,
                    ImportedAt = now,
                    UpdatedAt  = now,
                });
            }
        }

        // Update index stats
        index.SymbolCount = components.Length;
        index.LastImportAt = now;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Index {Index}: {Count} componenten geïmporteerd", index.DisplayName, components.Length);
        return components.Length;
    }
}
