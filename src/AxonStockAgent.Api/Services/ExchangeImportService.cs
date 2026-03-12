using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Providers;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class ExchangeImportService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<ExchangeImportService> _logger;

    public ExchangeImportService(AppDbContext db, ProviderManager providers, ILogger<ExchangeImportService> logger)
    {
        _db        = db;
        _providers = providers;
        _logger    = logger;
    }

    /// <summary>
    /// Importeert alle symbolen voor de opgegeven exchange vanuit EODHD.
    /// Upsert: bestaande symbolen worden bijgewerkt, nieuwe worden toegevoegd.
    /// </summary>
    public async Task<int> ImportExchangeSymbols(string exchangeCode)
    {
        // Haal de EODHD provider op
        var providerObj = await _providers.GetProviderByName("eodhd");
        if (providerObj is not EodhdProvider eodhd)
        {
            _logger.LogWarning("EODHD provider niet beschikbaar voor exchange import");
            return 0;
        }

        var symbols = await eodhd.GetExchangeSymbolsDetailed(exchangeCode);
        if (symbols.Length == 0)
        {
            _logger.LogWarning("Geen symbolen gevonden voor exchange {Exchange}", exchangeCode);
            return 0;
        }

        _logger.LogInformation("Importeer {Count} symbolen voor exchange {Exchange}", symbols.Length, exchangeCode);

        // Haal bestaande symbolen op voor deze exchange
        var existing = await _db.MarketSymbols
            .Where(m => m.Exchange == exchangeCode)
            .ToDictionaryAsync(m => m.Symbol);

        var now = DateTime.UtcNow;
        int added = 0, updated = 0;

        foreach (var s in symbols)
        {
            var fullSymbol = $"{s.Code}.{exchangeCode}";

            if (existing.TryGetValue(fullSymbol, out var entity))
            {
                // Update
                entity.Name = string.IsNullOrEmpty(s.Name) ? entity.Name : s.Name;
                entity.Country = string.IsNullOrEmpty(s.Country) ? entity.Country : s.Country;
                entity.Currency = string.IsNullOrEmpty(s.Currency) ? entity.Currency : s.Currency;
                entity.SymbolType = string.IsNullOrEmpty(s.Type) ? entity.SymbolType : s.Type;
                entity.IsActive = true;
                entity.UpdatedAt = now;
                updated++;
            }
            else
            {
                // Insert
                _db.MarketSymbols.Add(new MarketSymbolEntity
                {
                    Symbol     = fullSymbol,
                    Exchange   = exchangeCode,
                    Name       = s.Name,
                    Country    = s.Country,
                    Currency   = s.Currency,
                    SymbolType = s.Type,
                    IsActive   = true,
                    ImportedAt = now,
                    UpdatedAt  = now,
                });
                added++;
            }
        }

        // Markeer symbolen die niet meer in de listing staan als inactive
        var currentCodes = symbols.Select(s => $"{s.Code}.{exchangeCode}").ToHashSet();
        var toDeactivate = existing.Values.Where(e => !currentCodes.Contains(e.Symbol) && e.IsActive);
        foreach (var e in toDeactivate)
        {
            e.IsActive = false;
            e.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();

        // Update tracked exchange stats
        var tracked = await _db.TrackedExchanges.FirstOrDefaultAsync(t => t.ExchangeCode == exchangeCode);
        if (tracked != null)
        {
            tracked.SymbolCount = await _db.MarketSymbols.CountAsync(m => m.Exchange == exchangeCode && m.IsActive);
            tracked.LastImportAt = now;
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Exchange {Exchange}: {Added} nieuw, {Updated} bijgewerkt", exchangeCode, added, updated);
        return added + updated;
    }
}
