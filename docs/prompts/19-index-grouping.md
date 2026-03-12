# Prompt 19 — Index Grouping: Symbolen groeperen per beursindex

## Doel

Voeg index-ondersteuning toe zodat symbolen op het Markets-scherm gegroepeerd worden per index (AEX, AMX, AScX, S&P 500, NASDAQ-100, Dow Jones 30). Indexen worden automatisch opgehaald via EODHD. De admin kan indexen toevoegen/verwijderen en importeren. Op het Markets-scherm worden de kolommen per index getoond i.p.v. per exchange.

## Verificatie achteraf

```bash
cd src/AxonStockAgent.Api && dotnet build --nologo -v quiet
cd frontend && npx tsc --noEmit && npm run build
```

---

## Stap 1: Nieuw DB entity `MarketIndexEntity`

Maak `src/AxonStockAgent.Api/Data/Entities/MarketIndexEntity.cs`:

```csharp
namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een beursindex (bijv. AEX, S&P 500) die gevolgd wordt.
/// </summary>
public class MarketIndexEntity
{
    public int Id { get; set; }
    /// <summary>EODHD symboolcode, bijv. "AEX.INDX", "GSPC.INDX"</summary>
    public string IndexSymbol { get; set; } = "";
    /// <summary>Weergavenaam, bijv. "AEX 25", "S&P 500"</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gekoppelde exchange code, bijv. "AS", "US"</summary>
    public string ExchangeCode { get; set; } = "";
    public string Country { get; set; } = "";
    public int SymbolCount { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastImportAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

## Stap 2: Nieuw DB entity `IndexMembershipEntity`

Maak `src/AxonStockAgent.Api/Data/Entities/IndexMembershipEntity.cs`:

```csharp
namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Koppeling tussen een MarketSymbol en een MarketIndex.
/// Een symbool kan in meerdere indexen zitten.
/// </summary>
public class IndexMembershipEntity
{
    public int Id { get; set; }
    public int MarketIndexId { get; set; }
    public MarketIndexEntity MarketIndex { get; set; } = null!;
    /// <summary>Volledig symbool zoals in MarketSymbols, bijv. "ASML.AS"</summary>
    public string Symbol { get; set; } = "";
    public string? Name { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
```

## Stap 3: Registreer in `AppDbContext`

Open `src/AxonStockAgent.Api/Data/AppDbContext.cs`.

Voeg toe als properties:
```csharp
public DbSet<MarketIndexEntity> MarketIndices { get; set; }
public DbSet<IndexMembershipEntity> IndexMemberships { get; set; }
```

Voeg toe in `OnModelCreating`:
```csharp
// MarketIndices
modelBuilder.Entity<MarketIndexEntity>(e =>
{
    e.ToTable("market_indices");
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.IndexSymbol).IsUnique();
    e.Property(x => x.IndexSymbol).HasMaxLength(30);
    e.Property(x => x.DisplayName).HasMaxLength(100);
    e.Property(x => x.ExchangeCode).HasMaxLength(20);
    e.Property(x => x.Country).HasMaxLength(5);
});

// IndexMemberships
modelBuilder.Entity<IndexMembershipEntity>(e =>
{
    e.ToTable("index_memberships");
    e.HasKey(x => x.Id);
    e.HasIndex(x => new { x.MarketIndexId, x.Symbol }).IsUnique();
    e.HasOne(x => x.MarketIndex).WithMany().HasForeignKey(x => x.MarketIndexId).OnDelete(DeleteBehavior.Cascade);
    e.Property(x => x.Symbol).HasMaxLength(50);
    e.Property(x => x.Name).HasMaxLength(200);
    e.Property(x => x.Sector).HasMaxLength(100);
    e.Property(x => x.Industry).HasMaxLength(100);
});
```

## Stap 4: EF Migratie

```bash
cd src/AxonStockAgent.Api
dotnet ef migrations add AddMarketIndices
dotnet ef database update
```

---

## Stap 5: Breid `EodhdProvider` uit met index-componenten ophalen

Voeg deze methode toe aan `src/AxonStockAgent.Api/Providers/EodhdProvider.cs`:

```csharp
/// <summary>
/// Haalt de componenten van een index op via EODHD fundamentals API.
/// Bijv. "AEX.INDX" retourneert de 25 AEX-aandelen.
/// Response bevat Components object met per symbool: Code, Exchange, Name, Sector, Industry.
/// </summary>
public async Task<IndexComponentInfo[]> GetIndexComponents(string indexSymbol)
{
    await RateLimit();
    var url = $"{BaseUrl}/fundamentals/{indexSymbol}?api_token={_apiKey}&fmt=json&filter=Components";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Components", out var components))
            return Array.Empty<IndexComponentInfo>();

        var result = new List<IndexComponentInfo>();
        foreach (var prop in components.EnumerateObject())
        {
            var c = prop.Value;
            var code = SafeString(c, "Code");
            var exchange = SafeString(c, "Exchange");
            if (string.IsNullOrEmpty(code)) continue;

            result.Add(new IndexComponentInfo(
                Code:     code,
                Exchange: exchange,
                Name:     SafeString(c, "Name"),
                Sector:   SafeString(c, "Sector"),
                Industry: SafeString(c, "Industry")
            ));
        }
        return result.ToArray();
    }
    catch (Exception ex)
    {
        _logger.LogWarning("EODHD GetIndexComponents mislukt voor {Index}: {Message}", indexSymbol, ex.Message);
        return Array.Empty<IndexComponentInfo>();
    }
}
```

Voeg het record toe (bij de bestaande `ExchangeSymbolInfo` record):
```csharp
public record IndexComponentInfo(string Code, string Exchange, string Name, string Sector, string Industry);
```

---

## Stap 6: `IndexImportService`

Maak `src/AxonStockAgent.Api/Services/IndexImportService.cs`:

```csharp
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
    /// Upsert: bestaande memberships worden bijgewerkt, nieuwe toegevoegd, verdwenen verwijderd.
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
        _logger.LogInformation("Index {Index}: {Count} componenten ge\u00efmporteerd", index.DisplayName, components.Length);
        return components.Length;
    }
}
```

## Stap 7: DI registratie

Open `src/AxonStockAgent.Api/Program.cs`, voeg toe:
```csharp
builder.Services.AddScoped<IndexImportService>();
```

---

## Stap 8: Admin endpoints voor index-beheer

Voeg toe aan `src/AxonStockAgent.Api/Controllers/AdminController.cs`:

```csharp
// ── Market Indices ────────────────────────────────────────────────────────

[HttpGet("indices")]
public async Task<IActionResult> GetIndices()
{
    var indices = await _db.MarketIndices
        .OrderBy(i => i.Country)
        .ThenBy(i => i.DisplayName)
        .ToListAsync();
    return Ok(new { data = indices });
}

[HttpPost("indices")]
public async Task<IActionResult> AddIndex([FromBody] AddIndexRequest request)
{
    var exists = await _db.MarketIndices.AnyAsync(i => i.IndexSymbol == request.IndexSymbol);
    if (exists) return Conflict(new { error = $"Index '{request.IndexSymbol}' bestaat al" });

    var entity = new MarketIndexEntity
    {
        IndexSymbol  = request.IndexSymbol,
        DisplayName  = request.DisplayName ?? request.IndexSymbol,
        ExchangeCode = request.ExchangeCode ?? "",
        Country      = request.Country ?? "XX",
        IsEnabled    = true,
    };
    _db.MarketIndices.Add(entity);
    await _db.SaveChangesAsync();
    return Ok(new { data = entity });
}

[HttpDelete("indices/{id:int}")]
public async Task<IActionResult> DeleteIndex(int id)
{
    var index = await _db.MarketIndices.FindAsync(id);
    if (index == null) return NotFound();

    var memberships = await _db.IndexMemberships.Where(m => m.MarketIndexId == id).ToListAsync();
    _db.IndexMemberships.RemoveRange(memberships);
    _db.MarketIndices.Remove(index);
    await _db.SaveChangesAsync();
    return Ok(new { message = $"Index '{index.DisplayName}' verwijderd" });
}

[HttpPost("indices/{id:int}/import")]
public async Task<IActionResult> ImportIndexComponents(int id, [FromServices] IndexImportService importService)
{
    var index = await _db.MarketIndices.FindAsync(id);
    if (index == null) return NotFound();

    var count = await importService.ImportIndexComponents(id);
    return Ok(new { data = new { index = index.DisplayName, importedCount = count } });
}
```

Voeg het request record toe:
```csharp
public record AddIndexRequest(string IndexSymbol, string? DisplayName = null, string? ExchangeCode = null, string? Country = null);
```

Voeg de benodigde usings toe voor `MarketIndexEntity`, `IndexMembershipEntity`, `IndexImportService`.

---

## Stap 9: API endpoint — symbolen per index ophalen

Voeg toe aan `src/AxonStockAgent.Api/Controllers/ExchangesController.cs`:

```csharp
/// <summary>
/// Retourneert alle actieve indexen met hun componenten-symbolen.
/// Dit is wat de frontend gebruikt om het Markets-scherm per index te groeperen.
/// </summary>
[HttpGet("indices-with-symbols")]
public async Task<IActionResult> GetIndicesWithSymbols()
{
    var indices = await _db.MarketIndices
        .Where(i => i.IsEnabled)
        .OrderBy(i => i.Country)
        .ThenBy(i => i.DisplayName)
        .Select(i => new
        {
            i.Id,
            i.IndexSymbol,
            i.DisplayName,
            i.ExchangeCode,
            i.Country,
            i.SymbolCount,
            Symbols = _db.IndexMemberships
                .Where(m => m.MarketIndexId == i.Id)
                .Join(_db.MarketSymbols.Where(ms => ms.IsActive),
                    m => m.Symbol,
                    ms => ms.Symbol,
                    (m, ms) => new
                    {
                        ms.Symbol,
                        ms.Name,
                        ms.Exchange,
                        ms.Sector,
                        ms.Industry,
                        ms.Country,
                        ms.Logo,
                        ms.MarketCap
                    })
                .OrderBy(s => s.Symbol)
                .ToList()
        })
        .ToListAsync();

    return Ok(new { data = indices });
}
```

---

## Stap 10: Frontend types

Voeg toe aan `frontend/src/types/index.ts`:

```ts
export interface MarketIndex {
  id: number;
  indexSymbol: string;
  displayName: string;
  exchangeCode: string;
  country: string;
  symbolCount: number;
  symbols: MarketSymbol[];
}
```

## Stap 11: Frontend hook

Voeg toe aan `frontend/src/hooks/useApi.ts`:

```ts
export function useIndicesWithSymbols() {
  return useQuery({
    queryKey: ['exchanges', 'indices-with-symbols'],
    queryFn: () => api.get<ApiResponse<MarketIndex[]>>('/v1/exchanges/indices-with-symbols'),
    staleTime: 5 * 60 * 1000,
  });
}
```

Voeg `MarketIndex` toe aan de import van types.

---

## Stap 12: Herschrijf MarketsPage — kolommen per index

Open `frontend/src/pages/MarketsPage.tsx`.

De belangrijkste wijzigingen:

1. Importeer de nieuwe hook: `useIndicesWithSymbols`
2. Gebruik `useIndicesWithSymbols()` als primaire databron i.p.v. `useAllSymbols()`
3. De `exchangeGroups` useMemo wordt vervangen door de index-data: elke index wordt een kolom
4. Fallback: symbolen die in MarketSymbols staan maar in GEEN index zitten, worden gegroepeerd als "Overig {exchange}" kolommen (zodat je de bestaande exchange-import symbolen niet kwijtraakt)

Vervang de **data-loading** en **grouping** sectie van `MarketsPage` (de hooks en useMemo's). Behoud alle bestaande UI-componenten (Tile, ExchangeColumn, SymbolSearchDropdown, etc.).

Concreet, vervang de hooks sectie door:

```tsx
// In de imports bovenaan:
import { useAllSymbols, useBatchQuotes, useLatestSignalsPerSymbol, useIndicesWithSymbols } from '../hooks/useApi';
import type { MarketSymbol, Quote, LatestSignalPerSymbol, MarketIndex } from '../types';
```

En in de `MarketsPage` component, vervang de data-loading:

```tsx
  const { data: indicesData, isLoading: indicesLoading } = useIndicesWithSymbols();
  const { data: symbolsData, isLoading: symbolsLoading } = useAllSymbols();
  const isLoading = indicesLoading || symbolsLoading;

  const indices: MarketIndex[] = indicesData?.data ?? [];
  const allMarketSymbols: MarketSymbol[] = symbolsData?.data ?? [];

  // Combineer: alle symbolen uit indexen + alle MarketSymbols
  const allSymbols = useMemo(() => {
    const symbolSet = new Map<string, MarketSymbol>();
    // Eerst index-symbolen
    for (const idx of indices) {
      for (const sym of idx.symbols) {
        symbolSet.set(sym.symbol, sym);
      }
    }
    // Dan MarketSymbols die nog niet in een index zitten
    for (const sym of allMarketSymbols) {
      if (!symbolSet.has(sym.symbol)) {
        symbolSet.set(sym.symbol, sym);
      }
    }
    return Array.from(symbolSet.values());
  }, [indices, allMarketSymbols]);
```

En vervang de `exchangeGroups` useMemo door:

```tsx
  // Group: indexen als kolommen, plus "Overig" voor symbolen zonder index
  const columnGroups = useMemo(() => {
    // Welke symbolen zitten in minstens één index?
    const indexedSymbols = new Set<string>();
    for (const idx of indices) {
      for (const sym of idx.symbols) {
        indexedSymbols.add(sym.symbol);
      }
    }

    type ColumnGroup = { key: string; label: string; country: string; symbols: MarketSymbol[] };
    const groups: ColumnGroup[] = [];

    // Index-kolommen
    for (const idx of indices) {
      let syms = idx.symbols;

      if (search.trim()) {
        const q = search.toLowerCase();
        syms = syms.filter(s =>
          s.symbol.toLowerCase().includes(q) ||
          shortSymbol(s.symbol).toLowerCase().includes(q) ||
          (s.name?.toLowerCase().includes(q)) ||
          (s.sector?.toLowerCase().includes(q))
        );
      }

      if (verdictFilter) {
        syms = syms.filter(s => signalMap[s.symbol]?.finalVerdict === verdictFilter);
      }

      if (syms.length > 0) {
        groups.push({
          key: idx.indexSymbol,
          label: idx.displayName,
          country: idx.country,
          symbols: syms.sort((a, b) => shortSymbol(a.symbol).localeCompare(shortSymbol(b.symbol))),
        });
      }
    }

    // "Overig" kolommen: symbolen in MarketSymbols die in geen enkele index zitten
    const ungrouped = allMarketSymbols.filter(s => !indexedSymbols.has(s.symbol));
    if (ungrouped.length > 0) {
      let filteredUngrouped = ungrouped;
      if (search.trim()) {
        const q = search.toLowerCase();
        filteredUngrouped = filteredUngrouped.filter(s =>
          s.symbol.toLowerCase().includes(q) ||
          shortSymbol(s.symbol).toLowerCase().includes(q) ||
          (s.name?.toLowerCase().includes(q))
        );
      }
      if (verdictFilter) {
        filteredUngrouped = filteredUngrouped.filter(s => signalMap[s.symbol]?.finalVerdict === verdictFilter);
      }

      // Groepeer per exchange
      const byExchange: Record<string, MarketSymbol[]> = {};
      for (const sym of filteredUngrouped) {
        const ex = sym.exchange ?? 'Other';
        if (!byExchange[ex]) byExchange[ex] = [];
        byExchange[ex].push(sym);
      }
      for (const [ex, syms] of Object.entries(byExchange)) {
        if (syms.length > 0) {
          groups.push({
            key: `other-${ex}`,
            label: `Overig ${ex}`,
            country: syms[0]?.country ?? 'XX',
            symbols: syms.sort((a, b) => shortSymbol(a.symbol).localeCompare(shortSymbol(b.symbol))),
          });
        }
      }
    }

    return groups;
  }, [indices, allMarketSymbols, filtered, search, verdictFilter, signalMap]);
```

En in de render-sectie, vervang `exchangeGroups.map(...)` door:

```tsx
{columnGroups.map(({ key, label, country, symbols }) => (
  <ExchangeColumn
    key={key}
    exchange={label}
    country={country}
    symbols={symbols}
    quotes={quotes}
    signalMap={signalMap}
    onSymbolClick={symbol => navigate(`/stock/${symbol}`)}
  />
))}
```

Update ook de `exchangeGroups.length === 0` check naar `columnGroups.length === 0`.

---

## Stap 13: Admin Exchanges pagina — voeg index-beheer sectie toe

Open `frontend/src/pages/AdminExchangesPage.tsx`.

Voeg een nieuwe sectie toe **boven** de bestaande "Actieve Beurzen" sectie. Dit is voor index-beheer.

Voeg de volgende state + queries toe (naast de bestaande exchange logic):

```tsx
interface MarketIndexAdmin {
  id: number;
  indexSymbol: string;
  displayName: string;
  exchangeCode: string;
  country: string;
  symbolCount: number;
  isEnabled: boolean;
  lastImportAt: string | null;
}

const INDEX_PRESETS = [
  { symbol: 'AEX.INDX',  name: 'AEX 25',      exchange: 'AS', country: 'NL' },
  { symbol: 'AMX.INDX',  name: 'AMX Midcap',   exchange: 'AS', country: 'NL' },
  { symbol: 'ASCX.INDX', name: 'AScX Smallcap', exchange: 'AS', country: 'NL' },
  { symbol: 'GSPC.INDX', name: 'S&P 500',      exchange: 'US', country: 'US' },
  { symbol: 'NDX.INDX',  name: 'NASDAQ-100',   exchange: 'US', country: 'US' },
  { symbol: 'DJI.INDX',  name: 'Dow Jones 30',  exchange: 'US', country: 'US' },
  { symbol: 'GDAXI.INDX', name: 'DAX 40',       exchange: 'XETRA', country: 'DE' },
  { symbol: 'FCHI.INDX',  name: 'CAC 40',       exchange: 'PA', country: 'FR' },
  { symbol: 'FTSE.INDX',  name: 'FTSE 100',     exchange: 'LSE', country: 'GB' },
];
```

Voeg queries toe:
```tsx
const { data: indicesData, isLoading: indicesLoading } = useQuery({
  queryKey: ['admin', 'indices'],
  queryFn: () => api.get<{ data: MarketIndexAdmin[] }>('/v1/admin/indices'),
});

const addIndexMutation = useMutation({
  mutationFn: (idx: { indexSymbol: string; displayName: string; exchangeCode: string; country: string }) =>
    api.post('/v1/admin/indices', idx),
  onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] }),
});

const deleteIndexMutation = useMutation({
  mutationFn: (id: number) => api.delete(`/v1/admin/indices/${id}`),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  },
});

const [importingIndex, setImportingIndex] = useState<number | null>(null);

async function handleImportIndex(id: number) {
  setImportingIndex(id);
  try {
    await api.post(`/v1/admin/indices/${id}/import`, {});
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  } finally {
    setImportingIndex(null);
  }
}

const adminIndices: MarketIndexAdmin[] = (indicesData as any)?.data ?? [];
const trackedIndexSymbols = new Set(adminIndices.map(i => i.indexSymbol));
const availableIndices = INDEX_PRESETS.filter(p => !trackedIndexSymbols.has(p.symbol));
```

Render de sectie **boven** de exchanges sectie, met dezelfde stijl (tabel + preset-knoppen). Gebruik dezelfde UI-patronen als de exchange-sectie maar met "Indexen" als titel. Toon kolommen: Naam, Symbool, Beurs, Land, Componenten, Laatste import, Acties (importeer + verwijder).

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `src/.../Data/Entities/MarketIndexEntity.cs` | **Nieuw** |
| `src/.../Data/Entities/IndexMembershipEntity.cs` | **Nieuw** |
| `src/.../Data/AppDbContext.cs` | **Gewijzigd** — 2 nieuwe DbSets |
| `src/.../Providers/EodhdProvider.cs` | **Gewijzigd** — `GetIndexComponents` + record |
| `src/.../Services/IndexImportService.cs` | **Nieuw** |
| `src/.../Controllers/AdminController.cs` | **Gewijzigd** — 4 nieuwe index endpoints |
| `src/.../Controllers/ExchangesController.cs` | **Gewijzigd** — `indices-with-symbols` endpoint |
| `src/.../Program.cs` | **Gewijzigd** — DI registratie |
| EF Migration | **Nieuw** |
| `frontend/src/types/index.ts` | **Gewijzigd** — `MarketIndex` type |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** — `useIndicesWithSymbols` hook |
| `frontend/src/pages/MarketsPage.tsx` | **Gewijzigd** — kolommen per index |
| `frontend/src/pages/AdminExchangesPage.tsx` | **Gewijzigd** — index-beheer sectie |

## Na de prompt

1. Ga naar **Admin → Beurzen**
2. Bij de nieuwe "Indexen" sectie: klik op **+ AEX 25**, **+ AMX Midcap**, **+ AScX Smallcap**
3. Klik per index op het **import-icoontje**
4. Doe hetzelfde voor S&P 500, NASDAQ-100, Dow Jones 30
5. Ga naar **Markets** (/) — je ziet nu kolommen: AEX 25 | AMX Midcap | AScX Smallcap | S&P 500 | NASDAQ-100 | Dow Jones 30
