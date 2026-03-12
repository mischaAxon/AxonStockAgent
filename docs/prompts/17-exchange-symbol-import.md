# Prompt 17 — Exchange Symbol Import: Automatisch symbolen ophalen per beurs

## Doel

Maak het mogelijk om per beurs automatisch alle symbolen te importeren via EODHD. De admin configureert welke beurzen actief zijn. Bij import worden alle symbolen opgehaald en opgeslagen in een nieuwe `MarketSymbols` tabel. De Markets-pagina leest uit deze tabel in plaats van de oude Watchlist.

## Verificatie achteraf

```bash
cd src/AxonStockAgent.Api && dotnet build --nologo -v quiet
cd frontend && npx tsc --noEmit && npm run build
```

---

## Stap 1: Nieuw DB entity `MarketSymbolEntity`

Maak een nieuw bestand `src/AxonStockAgent.Api/Data/Entities/MarketSymbolEntity.cs`:

```csharp
namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een symbool geïmporteerd van een exchange-listing.
/// Los van WatchlistItem — dit zijn alle bekende symbolen per beurs.
/// </summary>
public class MarketSymbolEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string? Name { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? SymbolType { get; set; } // "Common Stock", "ETF", etc.
    public string? Logo { get; set; }
    public long? MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

## Stap 2: Nieuw DB entity `TrackedExchangeEntity`

Maak `src/AxonStockAgent.Api/Data/Entities/TrackedExchangeEntity.cs`:

```csharp
namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een beurs die de admin heeft ingeschakeld voor automatische symbol-import.
/// </summary>
public class TrackedExchangeEntity
{
    public int Id { get; set; }
    /// <summary>Exchange code zoals EODHD die gebruikt: "AS", "US", "XETRA", "LSE", etc.</summary>
    public string ExchangeCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    /// <summary>Aantal symbolen bij laatste import.</summary>
    public int SymbolCount { get; set; } = 0;
    public DateTime? LastImportAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

## Stap 3: Registreer in `AppDbContext`

Open `src/AxonStockAgent.Api/Data/AppDbContext.cs`.

Voeg toe als properties:

```csharp
public DbSet<MarketSymbolEntity> MarketSymbols { get; set; }
public DbSet<TrackedExchangeEntity> TrackedExchanges { get; set; }
```

Voeg toe in de `OnModelCreating` method:

```csharp
// MarketSymbols
modelBuilder.Entity<MarketSymbolEntity>(e =>
{
    e.ToTable("market_symbols");
    e.HasKey(x => x.Id);
    e.HasIndex(x => new { x.Symbol, x.Exchange }).IsUnique();
    e.HasIndex(x => x.Exchange);
    e.HasIndex(x => x.Country);
    e.Property(x => x.Symbol).HasMaxLength(50);
    e.Property(x => x.Exchange).HasMaxLength(20);
    e.Property(x => x.Name).HasMaxLength(200);
    e.Property(x => x.Sector).HasMaxLength(100);
    e.Property(x => x.Industry).HasMaxLength(100);
    e.Property(x => x.Country).HasMaxLength(5);
    e.Property(x => x.Currency).HasMaxLength(10);
    e.Property(x => x.SymbolType).HasMaxLength(50);
    e.Property(x => x.Logo).HasMaxLength(500);
});

// TrackedExchanges
modelBuilder.Entity<TrackedExchangeEntity>(e =>
{
    e.ToTable("tracked_exchanges");
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.ExchangeCode).IsUnique();
    e.Property(x => x.ExchangeCode).HasMaxLength(20);
    e.Property(x => x.DisplayName).HasMaxLength(100);
    e.Property(x => x.Country).HasMaxLength(5);
});
```

Vergeet niet de usings toe te voegen.

## Stap 4: EF Core Migratie

Voer uit:
```bash
cd src/AxonStockAgent.Api
dotnet ef migrations add AddMarketSymbolsAndTrackedExchanges
dotnet ef database update
```

Als `dotnet ef` niet beschikbaar is, gebruik de tool vanuit het juiste pad.

---

## Stap 5: Breid `EodhdProvider.GetSymbols` uit — retourneer meer data

De huidige `GetSymbols` methode retourneert alleen `string[]` (symboolcodes). We hebben meer velden nodig.

Voeg een nieuwe methode toe aan `EodhdProvider.cs` (naast de bestaande `GetSymbols`):

```csharp
/// <summary>
/// Haalt alle symbolen van een exchange op met naam, type en valuta.
/// EODHD exchange-symbol-list retourneert: Code, Name, Country, Exchange, Currency, Type.
/// </summary>
public async Task<ExchangeSymbolInfo[]> GetExchangeSymbolsDetailed(string exchange)
{
    await RateLimit();
    var url = $"{BaseUrl}/exchange-symbol-list/{exchange}?api_token={_apiKey}&fmt=json&type=common_stock";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<ExchangeSymbolInfo>();

        return doc.RootElement.EnumerateArray()
            .Select(s => new ExchangeSymbolInfo(
                Code:     SafeString(s, "Code"),
                Name:     SafeString(s, "Name"),
                Country:  SafeString(s, "Country"),
                Exchange: SafeString(s, "Exchange"),
                Currency: SafeString(s, "Currency"),
                Type:     SafeString(s, "Type")
            ))
            .Where(s => !string.IsNullOrEmpty(s.Code))
            .ToArray();
    }
    catch (Exception ex)
    {
        _logger.LogWarning("EODHD GetExchangeSymbolsDetailed mislukt voor {Exchange}: {Message}", exchange, ex.Message);
        return Array.Empty<ExchangeSymbolInfo>();
    }
}
```

Voeg het record toe onderaan het bestand (of in een apart bestand in Core/Models):

```csharp
public record ExchangeSymbolInfo(string Code, string Name, string Country, string Exchange, string Currency, string Type);
```

---

## Stap 6: `ExchangeImportService` — de import-logica

Maak `src/AxonStockAgent.Api/Services/ExchangeImportService.cs`:

```csharp
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
```

## Stap 7: Registreer `ExchangeImportService` in DI

Open `src/AxonStockAgent.Api/Program.cs` en voeg toe bij de service-registraties:

```csharp
builder.Services.AddScoped<ExchangeImportService>();
```

---

## Stap 8: Admin API endpoints voor exchange-beheer

Voeg de volgende endpoints toe aan `src/AxonStockAgent.Api/Controllers/AdminController.cs`:

```csharp
// ── Tracked Exchanges ──────────────────────────────────────────────────────

[HttpGet("exchanges")]
public async Task<IActionResult> GetTrackedExchanges()
{
    var exchanges = await _db.TrackedExchanges
        .OrderBy(e => e.Country)
        .ThenBy(e => e.DisplayName)
        .ToListAsync();
    return Ok(new { data = exchanges });
}

[HttpPost("exchanges")]
public async Task<IActionResult> AddTrackedExchange([FromBody] AddExchangeRequest request)
{
    var exists = await _db.TrackedExchanges.AnyAsync(e => e.ExchangeCode == request.ExchangeCode);
    if (exists) return Conflict(new { error = $"Exchange '{request.ExchangeCode}' bestaat al" });

    var entity = new TrackedExchangeEntity
    {
        ExchangeCode = request.ExchangeCode,
        DisplayName  = request.DisplayName ?? request.ExchangeCode,
        Country      = request.Country ?? "XX",
        IsEnabled    = true,
    };
    _db.TrackedExchanges.Add(entity);
    await _db.SaveChangesAsync();

    return Ok(new { data = entity });
}

[HttpPut("exchanges/{id:int}")]
public async Task<IActionResult> UpdateTrackedExchange(int id, [FromBody] UpdateExchangeRequest request)
{
    var exchange = await _db.TrackedExchanges.FindAsync(id);
    if (exchange == null) return NotFound();

    if (request.IsEnabled.HasValue) exchange.IsEnabled = request.IsEnabled.Value;
    if (request.DisplayName != null) exchange.DisplayName = request.DisplayName;

    await _db.SaveChangesAsync();
    return Ok(new { data = exchange });
}

[HttpDelete("exchanges/{id:int}")]
public async Task<IActionResult> DeleteTrackedExchange(int id)
{
    var exchange = await _db.TrackedExchanges.FindAsync(id);
    if (exchange == null) return NotFound();

    // Verwijder ook alle geïmporteerde symbolen voor deze beurs
    var symbols = await _db.MarketSymbols.Where(m => m.Exchange == exchange.ExchangeCode).ToListAsync();
    _db.MarketSymbols.RemoveRange(symbols);
    _db.TrackedExchanges.Remove(exchange);
    await _db.SaveChangesAsync();

    return Ok(new { message = $"Exchange '{exchange.ExchangeCode}' verwijderd met {symbols.Count} symbolen" });
}

[HttpPost("exchanges/{id:int}/import")]
public async Task<IActionResult> ImportExchangeSymbols(int id, [FromServices] ExchangeImportService importService)
{
    var exchange = await _db.TrackedExchanges.FindAsync(id);
    if (exchange == null) return NotFound();

    var count = await importService.ImportExchangeSymbols(exchange.ExchangeCode);
    return Ok(new { data = new { exchange = exchange.ExchangeCode, importedCount = count } });
}
```

Voeg de request records toe onderaan het bestand:

```csharp
public record AddExchangeRequest(string ExchangeCode, string? DisplayName = null, string? Country = null);
public record UpdateExchangeRequest(bool? IsEnabled = null, string? DisplayName = null);
```

Vergeet niet de using voor `ExchangeImportService` en `TrackedExchangeEntity` + `MarketSymbolEntity` toe te voegen.

---

## Stap 9: Wijzig `ExchangesController` — lees uit `MarketSymbols` i.p.v. `Watchlist`

Open `src/AxonStockAgent.Api/Controllers/ExchangesController.cs`.

Vervang de **volledige inhoud** door:

```csharp
using AxonStockAgent.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ExchangesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExchangesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Retourneert alle beurzen met hun symbooltellingen, gegroepeerd per land.
    /// Leest uit TrackedExchanges (admin-geconfigureerd) + MarketSymbols.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exchanges = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .GroupBy(m => new { m.Exchange, m.Country })
            .Select(g => new
            {
                Exchange = g.Key.Exchange,
                Country = g.Key.Country ?? "XX",
                SymbolCount = g.Count()
            })
            .OrderBy(e => e.Country)
            .ThenBy(e => e.Exchange)
            .ToListAsync();

        return Ok(new { data = exchanges });
    }

    /// <summary>
    /// Retourneert alle symbolen voor een specifieke beurs.
    /// </summary>
    [HttpGet("{exchange}/symbols")]
    public async Task<IActionResult> GetSymbols(string exchange)
    {
        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive && m.Exchange == exchange)
            .OrderBy(m => m.Symbol)
            .Select(m => new
            {
                m.Symbol,
                m.Name,
                m.Sector,
                m.Industry,
                m.Country,
                m.Logo,
                m.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }

    /// <summary>
    /// Retourneert ALLE actieve symbolen uit MarketSymbols.
    /// Optioneel filteren op country of exchange.
    /// </summary>
    [HttpGet("all-symbols")]
    public async Task<IActionResult> GetAllSymbols([FromQuery] string? country = null, [FromQuery] string? exchange = null)
    {
        var query = _db.MarketSymbols.Where(m => m.IsActive);

        if (!string.IsNullOrEmpty(country))
            query = query.Where(m => m.Country == country);
        if (!string.IsNullOrEmpty(exchange))
            query = query.Where(m => m.Exchange == exchange);

        var symbols = await query
            .OrderBy(m => m.Country)
            .ThenBy(m => m.Exchange)
            .ThenBy(m => m.Symbol)
            .Select(m => new
            {
                m.Symbol,
                m.Name,
                Exchange = m.Exchange,
                m.Sector,
                m.Industry,
                m.Country,
                m.Logo,
                m.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }
}
```

---

## Stap 10: Admin UI — Exchange Management pagina

Maak `frontend/src/pages/AdminExchangesPage.tsx`:

```tsx
import { useState } from 'react';
import { Plus, Trash2, Download, Globe } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';

interface TrackedExchange {
  id: number;
  exchangeCode: string;
  displayName: string;
  country: string;
  isEnabled: boolean;
  symbolCount: number;
  lastImportAt: string | null;
}

// Veelgebruikte EODHD exchange codes
const EXCHANGE_PRESETS = [
  { code: 'AS', name: 'Euronext Amsterdam', country: 'NL' },
  { code: 'US', name: 'NYSE + NASDAQ (gecombineerd)', country: 'US' },
  { code: 'XETRA', name: 'XETRA (Frankfurt)', country: 'DE' },
  { code: 'LSE', name: 'London Stock Exchange', country: 'GB' },
  { code: 'PA', name: 'Euronext Paris', country: 'FR' },
  { code: 'MI', name: 'Borsa Italiana (Milan)', country: 'IT' },
  { code: 'SW', name: 'SIX Swiss Exchange', country: 'CH' },
  { code: 'TO', name: 'Toronto Stock Exchange', country: 'CA' },
  { code: 'HK', name: 'Hong Kong Stock Exchange', country: 'HK' },
  { code: 'TSE', name: 'Tokyo Stock Exchange', country: 'JP' },
  { code: 'BR', name: 'Euronext Brussels', country: 'BE' },
  { code: 'SN', name: 'Bolsa de Madrid', country: 'ES' },
  { code: 'ST', name: 'Nasdaq Stockholm', country: 'SE' },
  { code: 'CO', name: 'Nasdaq Copenhagen', country: 'DK' },
  { code: 'HE', name: 'Nasdaq Helsinki', country: 'FI' },
  { code: 'OL', name: 'Oslo Børs', country: 'NO' },
];

export default function AdminExchangesPage() {
  const queryClient = useQueryClient();
  const [importing, setImporting] = useState<number | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'exchanges'],
    queryFn: () => api.get<{ data: TrackedExchange[] }>('/v1/admin/exchanges'),
  });

  const addMutation = useMutation({
    mutationFn: (exchange: { exchangeCode: string; displayName: string; country: string }) =>
      api.post('/v1/admin/exchanges', exchange),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.delete(`/v1/admin/exchanges/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isEnabled }: { id: number; isEnabled: boolean }) =>
      api.put(`/v1/admin/exchanges/${id}`, { isEnabled }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] }),
  });

  async function handleImport(id: number) {
    setImporting(id);
    try {
      await api.post(`/v1/admin/exchanges/${id}/import`, {});
      queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    } finally {
      setImporting(null);
    }
  }

  const exchanges: TrackedExchange[] = (data as any)?.data ?? [];
  const trackedCodes = new Set(exchanges.map(e => e.exchangeCode));
  const available = EXCHANGE_PRESETS.filter(p => !trackedCodes.has(p.code));

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Exchange Beheer</h2>
      <p className="text-sm text-gray-400 mb-6">
        Configureer welke beurzen automatisch worden geïmporteerd via EODHD.
        Klik "Importeer" om alle symbolen voor een beurs op te halen.
      </p>

      {/* Active exchanges */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden mb-6">
        <div className="px-4 py-3 border-b border-gray-800">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Actieve Beurzen</h3>
        </div>

        {isLoading ? (
          <div className="p-4 text-gray-500 text-sm">Laden...</div>
        ) : exchanges.length === 0 ? (
          <div className="p-8 text-center">
            <Globe size={32} className="text-gray-700 mx-auto mb-3" />
            <p className="text-gray-400">Geen beurzen geconfigureerd.</p>
            <p className="text-gray-600 text-sm">Voeg een beurs toe hieronder.</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-2 text-left text-gray-400">Beurs</th>
                <th className="px-4 py-2 text-left text-gray-400">Code</th>
                <th className="px-4 py-2 text-left text-gray-400">Land</th>
                <th className="px-4 py-2 text-right text-gray-400">Symbolen</th>
                <th className="px-4 py-2 text-left text-gray-400">Laatste import</th>
                <th className="px-4 py-2 text-center text-gray-400">Acties</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {exchanges.map(ex => (
                <tr key={ex.id} className="hover:bg-gray-800/30">
                  <td className="px-4 py-3 text-white font-medium">{ex.displayName}</td>
                  <td className="px-4 py-3 font-mono text-axon-400">{ex.exchangeCode}</td>
                  <td className="px-4 py-3 text-gray-400">{ex.country}</td>
                  <td className="px-4 py-3 text-right font-mono text-gray-300">{ex.symbolCount.toLocaleString()}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">
                    {ex.lastImportAt ? new Date(ex.lastImportAt).toLocaleString('nl-NL') : 'Nooit'}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        onClick={() => toggleMutation.mutate({ id: ex.id, isEnabled: !ex.isEnabled })}
                        className={`px-2 py-1 rounded text-xs font-medium ${
                          ex.isEnabled
                            ? 'bg-green-500/15 text-green-400'
                            : 'bg-gray-700 text-gray-500'
                        }`}
                      >
                        {ex.isEnabled ? 'Actief' : 'Inactief'}
                      </button>
                      <button
                        onClick={() => handleImport(ex.id)}
                        disabled={importing === ex.id}
                        className="p-1.5 rounded bg-blue-500/15 text-blue-400 hover:bg-blue-500/25 transition-colors disabled:opacity-50"
                        title="Importeer symbolen"
                      >
                        <Download size={14} className={importing === ex.id ? 'animate-bounce' : ''} />
                      </button>
                      <button
                        onClick={() => { if (window.confirm(`Beurs ${ex.exchangeCode} verwijderen inclusief alle symbolen?`)) deleteMutation.mutate(ex.id); }}
                        className="p-1.5 rounded text-gray-600 hover:text-red-400 transition-colors"
                        title="Verwijder beurs"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Add exchange */}
      {available.length > 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Beurs Toevoegen</h3>
          <div className="flex flex-wrap gap-2">
            {available.map(preset => (
              <button
                key={preset.code}
                onClick={() => addMutation.mutate({
                  exchangeCode: preset.code,
                  displayName: preset.name,
                  country: preset.country,
                })}
                disabled={addMutation.isPending}
                className="px-3 py-2 rounded-lg bg-gray-800 border border-gray-700 text-sm text-gray-300 hover:border-axon-500 hover:text-white transition-all flex items-center gap-2"
              >
                <Plus size={14} />
                <span className="font-mono text-axon-400">{preset.code}</span>
                <span className="text-gray-500">{preset.name}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
```

## Stap 11: Route + nav voor AdminExchangesPage

### 11a. App.tsx

Voeg import toe:
```tsx
import AdminExchangesPage from './pages/AdminExchangesPage';
```

Voeg route toe binnen de admin-only routes:
```tsx
<Route
  path="/admin/exchanges"
  element={
    <ProtectedRoute requireAdmin>
      <AdminExchangesPage />
    </ProtectedRoute>
  }
/>
```

### 11b. Layout.tsx

Voeg `Globe` icon import toe (als die er nog niet is).

Voeg toe aan de `adminItems` array:
```tsx
{ to: '/admin/exchanges', label: 'Beurzen',   icon: Globe    },
```

Zorg dat `Globe` niet dubbel geïmporteerd wordt — het staat al in de lucide-react import voor de main nav.

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `src/.../Data/Entities/MarketSymbolEntity.cs` | **Nieuw** |
| `src/.../Data/Entities/TrackedExchangeEntity.cs` | **Nieuw** |
| `src/.../Data/AppDbContext.cs` | **Gewijzigd** — 2 nieuwe DbSets + model config |
| `src/.../Providers/EodhdProvider.cs` | **Gewijzigd** — `GetExchangeSymbolsDetailed` + record |
| `src/.../Services/ExchangeImportService.cs` | **Nieuw** |
| `src/.../Controllers/AdminController.cs` | **Gewijzigd** — 5 nieuwe exchange endpoints |
| `src/.../Controllers/ExchangesController.cs` | **Herschreven** — leest uit MarketSymbols |
| `src/.../Program.cs` | **Gewijzigd** — DI registratie |
| `frontend/src/pages/AdminExchangesPage.tsx` | **Nieuw** |
| `frontend/src/App.tsx` | **Gewijzigd** — nieuwe admin route |
| `frontend/src/components/layout/Layout.tsx` | **Gewijzigd** — nieuwe admin nav item |
| EF Migration | **Nieuw** — `AddMarketSymbolsAndTrackedExchanges` |

## Na de prompt

Na succesvolle uitvoering:
1. Ga naar Admin → Beurzen
2. Voeg AMS (Amsterdam) toe
3. Klik "Importeer" — alle AMS-symbolen worden geladen
4. Ga naar Markets (/) — je ziet nu alle Amsterdam-symbolen als cards
5. Herhaal voor andere beurzen naar wens
