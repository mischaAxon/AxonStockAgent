# Prompt 03 — Bedrijfsdata: Fundamentals, Analyst Ratings, Insider Trading

**Issue:** #6  
**Branch:** maak een nieuwe branch `feature/fundamentals` vanaf `main`  
**Doel:** Per aandeel uitgebreide bedrijfsdata verzamelen, opslaan en tonen  

---

## Context

Lees deze bestanden eerst zodat je de architectuur begrijpt:
- `docs/HANDOVER_SESSION_1.md` — volledige projectcontext
- `src/AxonStockAgent.Core/Interfaces/IFundamentalsProvider.cs` — bestaande interface
- `src/AxonStockAgent.Api/Providers/FinnhubProvider.cs` — bestaande provider (implementeert al GetProfile + GetAnalystRatings)
- `src/AxonStockAgent.Api/Services/SectorService.cs` — enrichment patroon
- `src/AxonStockAgent.Api/Services/ProviderManager.cs` — factory patroon
- `src/AxonStockAgent.Api/Data/AppDbContext.cs` — DbContext met entity configs
- `frontend/src/types/index.ts` — bestaande TypeScript types
- `frontend/src/hooks/useApi.ts` — bestaande API hooks
- `frontend/src/pages/WatchlistPage.tsx` — huidige watchlist pagina

Volg de bestaande patronen exact: entity → DbContext → service → controller → frontend hooks → pagina.

---

## Deel 1: Core Interface Uitbreiden

**Bestand:** `src/AxonStockAgent.Core/Interfaces/IFundamentalsProvider.cs`

Voeg deze records toe BOVEN de interface definitie (behoud de bestaande CompanyProfile en AnalystRating):

```csharp
public record FinancialMetrics(
    string Symbol,
    // Valuation
    double? PeRatio,             // Price/Earnings
    double? ForwardPe,           // Forward P/E
    double? PbRatio,             // Price/Book
    double? PsRatio,             // Price/Sales
    double? EvToEbitda,          // EV/EBITDA
    // Profitability
    double? ProfitMargin,        // Net profit margin (0-1)
    double? OperatingMargin,
    double? ReturnOnEquity,      // ROE (0-1)
    double? ReturnOnAssets,      // ROA (0-1)
    // Growth
    double? RevenueGrowthYoy,    // Year-over-year (0-1)
    double? EarningsGrowthYoy,
    // Balance sheet
    double? DebtToEquity,
    double? CurrentRatio,
    double? QuickRatio,
    // Dividends
    double? DividendYield,       // (0-1)
    double? PayoutRatio,
    // Size
    double? MarketCap,
    double? Revenue,
    double? NetIncome,
    long? SharesOutstanding,
    // Meta
    DateTime FetchedAt
);

public record InsiderTransaction(
    string Symbol,
    string Name,           // insider naam
    string Relation,       // CEO, CFO, Director, etc.
    string TransactionType, // "buy", "sell", "exercise"
    DateTime Date,
    long Shares,
    double PricePerShare,
    double TotalValue
);

public record PriceTarget(
    string Symbol,
    double TargetHigh,
    double TargetLow,
    double TargetMean,
    double TargetMedian,
    int NumberOfAnalysts,
    DateTime FetchedAt
);
```

Voeg deze methodes toe aan de `IFundamentalsProvider` interface:

```csharp
public interface IFundamentalsProvider
{
    string Name { get; }
    Task<CompanyProfile?> GetProfile(string symbol);
    Task<AnalystRating?> GetAnalystRatings(string symbol);
    // Nieuw:
    Task<FinancialMetrics?> GetFinancialMetrics(string symbol);
    Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3);
    Task<PriceTarget?> GetPriceTarget(string symbol);
}
```

---

## Deel 2: FinnhubProvider Uitbreiden

**Bestand:** `src/AxonStockAgent.Api/Providers/FinnhubProvider.cs`

Implementeer de 3 nieuwe methodes. Gebruik de bestaande `RateLimit()` en error handling patronen.

### GetFinancialMetrics

Finnhub endpoint: `GET /stock/metric?symbol={symbol}&metric=all&token={apiKey}`

Het response bevat een `metric` object met velden als: `peBasicExclExtraTTM`, `psTTM`, `pbQuarterly`, `currentRatioQuarterly`, `netProfitMarginTTM`, `roeTTM`, `roaTTM`, `revenueGrowthQuarterlyYoy`, `52WeekHigh`, etc.

```csharp
public async Task<FinancialMetrics?> GetFinancialMetrics(string symbol)
{
    await RateLimit();
    var url = $"{BaseUrl}/stock/metric?symbol={symbol}&metric=all&token={_apiKey}";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("metric", out var m)) return null;

        return new FinancialMetrics(
            Symbol:             symbol,
            PeRatio:            GetNullableDouble(m, "peBasicExclExtraTTM"),
            ForwardPe:          GetNullableDouble(m, "peExclExtraAnnual"),
            PbRatio:            GetNullableDouble(m, "pbQuarterly"),
            PsRatio:            GetNullableDouble(m, "psTTM"),
            EvToEbitda:         GetNullableDouble(m, "currentEv/freeCashFlowTTM"), // Finnhub heeft geen directe EV/EBITDA; gebruik null
            ProfitMargin:       GetNullableDouble(m, "netProfitMarginTTM"),
            OperatingMargin:    GetNullableDouble(m, "operatingMarginTTM"),
            ReturnOnEquity:     GetNullableDouble(m, "roeTTM"),
            ReturnOnAssets:     GetNullableDouble(m, "roaTTM"),
            RevenueGrowthYoy:   GetNullableDouble(m, "revenueGrowthQuarterlyYoy"),
            EarningsGrowthYoy:  GetNullableDouble(m, "epsGrowthQuarterlyYoy"),
            DebtToEquity:       GetNullableDouble(m, "totalDebt/totalEquityQuarterly"),
            CurrentRatio:       GetNullableDouble(m, "currentRatioQuarterly"),
            QuickRatio:         GetNullableDouble(m, "quickRatioQuarterly"),
            DividendYield:      GetNullableDouble(m, "dividendYieldIndicatedAnnual"),
            PayoutRatio:        GetNullableDouble(m, "payoutRatioTTM"),
            MarketCap:          GetNullableDouble(m, "marketCapitalization"),
            Revenue:            GetNullableDouble(m, "revenueTTM"),
            NetIncome:          GetNullableDouble(m, "netIncomeTTM"),
            SharesOutstanding:  null, // niet in dit endpoint
            FetchedAt:          DateTime.UtcNow
        );
    }
    catch (Exception ex)
    {
        _logger.LogWarning("GetFinancialMetrics mislukt voor {Symbol}: {Message}", symbol, ex.Message);
        return null;
    }
}

// Helper methode (voeg toe als private method)
private static double? GetNullableDouble(JsonElement element, string property)
{
    if (!element.TryGetProperty(property, out var prop)) return null;
    if (prop.ValueKind == JsonValueKind.Null) return null;
    try { return prop.GetDouble(); } catch { return null; }
}
```

### GetInsiderTransactions

Finnhub endpoint: `GET /stock/insider-transactions?symbol={symbol}&token={apiKey}`

```csharp
public async Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3)
{
    await RateLimit();
    var url = $"{BaseUrl}/stock/insider-transactions?symbol={symbol}&token={_apiKey}";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data)) return Array.Empty<InsiderTransaction>();

        var cutoff = DateTime.UtcNow.AddMonths(-months);

        return data.EnumerateArray()
            .Select(t =>
            {
                var dateStr = t.TryGetProperty("transactionDate", out var d) ? d.GetString() : null;
                if (!DateTime.TryParse(dateStr, out var date)) return null;
                if (date < cutoff) return null;

                var shares = t.TryGetProperty("share", out var sh) ? sh.GetInt64() : 0;
                var price  = t.TryGetProperty("transactionPrice", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetDouble() : 0;

                return new InsiderTransaction(
                    Symbol:          symbol,
                    Name:            t.TryGetProperty("name",               out var n)  ? n.GetString()  ?? "" : "",
                    Relation:        t.TryGetProperty("filingRelation",     out var r)  ? r.GetString()  ?? "" : "",
                    TransactionType: t.TryGetProperty("transactionType",    out var tt) ? tt.GetString() ?? "" : "",
                    Date:            date,
                    Shares:          Math.Abs(shares),
                    PricePerShare:   price,
                    TotalValue:      Math.Abs(shares) * price
                );
            })
            .Where(t => t != null)
            .Cast<InsiderTransaction>()
            .OrderByDescending(t => t.Date)
            .Take(50)
            .ToArray();
    }
    catch (Exception ex)
    {
        _logger.LogWarning("GetInsiderTransactions mislukt voor {Symbol}: {Message}", symbol, ex.Message);
        return Array.Empty<InsiderTransaction>();
    }
}
```

### GetPriceTarget

Finnhub endpoint: `GET /stock/price-target?symbol={symbol}&token={apiKey}`

```csharp
public async Task<PriceTarget?> GetPriceTarget(string symbol)
{
    await RateLimit();
    var url = $"{BaseUrl}/stock/price-target?symbol={symbol}&token={_apiKey}";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;

        if (!r.TryGetProperty("targetHigh", out _)) return null;

        return new PriceTarget(
            Symbol:           symbol,
            TargetHigh:       r.TryGetProperty("targetHigh",   out var h) ? h.GetDouble() : 0,
            TargetLow:        r.TryGetProperty("targetLow",    out var l) ? l.GetDouble() : 0,
            TargetMean:       r.TryGetProperty("targetMean",   out var m) ? m.GetDouble() : 0,
            TargetMedian:     r.TryGetProperty("targetMedian", out var md) ? md.GetDouble() : 0,
            NumberOfAnalysts: r.TryGetProperty("lastUpdated",  out _) ? 0 : 0, // Finnhub geeft geen count; we zetten 0
            FetchedAt:        DateTime.UtcNow
        );
    }
    catch (Exception ex)
    {
        _logger.LogWarning("GetPriceTarget mislukt voor {Symbol}: {Message}", symbol, ex.Message);
        return null;
    }
}
```

---

## Deel 3: Database Entities

### CompanyFundamentalsEntity

**Nieuw bestand:** `src/AxonStockAgent.Api/Data/Entities/CompanyFundamentalsEntity.cs`

```csharp
using System;

namespace AxonStockAgent.Api.Data.Entities;

public class CompanyFundamentalsEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;

    // Valuation
    public double? PeRatio { get; set; }
    public double? ForwardPe { get; set; }
    public double? PbRatio { get; set; }
    public double? PsRatio { get; set; }
    public double? EvToEbitda { get; set; }

    // Profitability
    public double? ProfitMargin { get; set; }
    public double? OperatingMargin { get; set; }
    public double? ReturnOnEquity { get; set; }
    public double? ReturnOnAssets { get; set; }

    // Growth
    public double? RevenueGrowthYoy { get; set; }
    public double? EarningsGrowthYoy { get; set; }

    // Balance sheet
    public double? DebtToEquity { get; set; }
    public double? CurrentRatio { get; set; }
    public double? QuickRatio { get; set; }

    // Dividends
    public double? DividendYield { get; set; }
    public double? PayoutRatio { get; set; }

    // Size
    public double? MarketCap { get; set; }
    public double? Revenue { get; set; }
    public double? NetIncome { get; set; }
    public long? SharesOutstanding { get; set; }

    // Analyst
    public int? AnalystBuy { get; set; }
    public int? AnalystHold { get; set; }
    public int? AnalystSell { get; set; }
    public int? AnalystStrongBuy { get; set; }
    public int? AnalystStrongSell { get; set; }
    public double? TargetPriceHigh { get; set; }
    public double? TargetPriceLow { get; set; }
    public double? TargetPriceMean { get; set; }
    public double? TargetPriceMedian { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### InsiderTransactionEntity

**Nieuw bestand:** `src/AxonStockAgent.Api/Data/Entities/InsiderTransactionEntity.cs`

```csharp
using System;

namespace AxonStockAgent.Api.Data.Entities;

public class InsiderTransactionEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public long Shares { get; set; }
    public double PricePerShare { get; set; }
    public double TotalValue { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Deel 4: AppDbContext Uitbreiden

**Bestand:** `src/AxonStockAgent.Api/Data/AppDbContext.cs`

Voeg toe:

```csharp
// Nieuwe DbSets
public DbSet<CompanyFundamentalsEntity> CompanyFundamentals => Set<CompanyFundamentalsEntity>();
public DbSet<InsiderTransactionEntity> InsiderTransactions => Set<InsiderTransactionEntity>();
```

Voeg toe in `OnModelCreating`:

```csharp
modelBuilder.Entity<CompanyFundamentalsEntity>(e =>
{
    e.ToTable("company_fundamentals");
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.Symbol).IsUnique();
    e.Property(x => x.FetchedAt).HasDefaultValueSql("NOW()");
    e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
});

modelBuilder.Entity<InsiderTransactionEntity>(e =>
{
    e.ToTable("insider_transactions");
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.Symbol);
    e.HasIndex(x => x.TransactionDate);
    e.HasIndex(x => new { x.Symbol, x.Name, x.TransactionDate }).IsUnique();
    e.Property(x => x.FetchedAt).HasDefaultValueSql("NOW()");
});
```

---

## Deel 5: Database SQL

**Bestand:** `database/init.sql`

Voeg onderaan toe:

```sql
-- Company fundamentals (gecacht, 1 rij per symbool)
CREATE TABLE IF NOT EXISTS company_fundamentals (
    id                    SERIAL PRIMARY KEY,
    symbol                VARCHAR(20) NOT NULL UNIQUE,
    pe_ratio              DOUBLE PRECISION,
    forward_pe            DOUBLE PRECISION,
    pb_ratio              DOUBLE PRECISION,
    ps_ratio              DOUBLE PRECISION,
    ev_to_ebitda          DOUBLE PRECISION,
    profit_margin         DOUBLE PRECISION,
    operating_margin      DOUBLE PRECISION,
    return_on_equity      DOUBLE PRECISION,
    return_on_assets      DOUBLE PRECISION,
    revenue_growth_yoy    DOUBLE PRECISION,
    earnings_growth_yoy   DOUBLE PRECISION,
    debt_to_equity        DOUBLE PRECISION,
    current_ratio         DOUBLE PRECISION,
    quick_ratio           DOUBLE PRECISION,
    dividend_yield        DOUBLE PRECISION,
    payout_ratio          DOUBLE PRECISION,
    market_cap            DOUBLE PRECISION,
    revenue               DOUBLE PRECISION,
    net_income            DOUBLE PRECISION,
    shares_outstanding    BIGINT,
    analyst_buy           INTEGER,
    analyst_hold          INTEGER,
    analyst_sell          INTEGER,
    analyst_strong_buy    INTEGER,
    analyst_strong_sell   INTEGER,
    target_price_high     DOUBLE PRECISION,
    target_price_low      DOUBLE PRECISION,
    target_price_mean     DOUBLE PRECISION,
    target_price_median   DOUBLE PRECISION,
    fetched_at            TIMESTAMPTZ DEFAULT NOW(),
    updated_at            TIMESTAMPTZ DEFAULT NOW()
);

-- Insider transactions
CREATE TABLE IF NOT EXISTS insider_transactions (
    id                SERIAL PRIMARY KEY,
    symbol            VARCHAR(20) NOT NULL,
    name              VARCHAR(200) NOT NULL,
    relation          VARCHAR(100),
    transaction_type  VARCHAR(20) NOT NULL,
    transaction_date  DATE NOT NULL,
    shares            BIGINT NOT NULL,
    price_per_share   DOUBLE PRECISION,
    total_value       DOUBLE PRECISION,
    fetched_at        TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(symbol, name, transaction_date)
);

CREATE INDEX IF NOT EXISTS idx_insider_symbol ON insider_transactions(symbol);
CREATE INDEX IF NOT EXISTS idx_insider_date   ON insider_transactions(transaction_date DESC);
```

---

## Deel 6: FundamentalsService

**Nieuw bestand:** `src/AxonStockAgent.Api/Services/FundamentalsService.cs`

```csharp
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class FundamentalsService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<FundamentalsService> _logger;

    // Cache TTL: data ouder dan dit wordt opnieuw opgehaald
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public FundamentalsService(AppDbContext db, ProviderManager providers, ILogger<FundamentalsService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Haalt fundamentals op voor een symbool. Gebruikt cache, refresht als ouder dan 24 uur.
    /// </summary>
    public async Task<CompanyFundamentalsEntity?> GetFundamentals(string symbol, bool forceRefresh = false)
    {
        var cached = await _db.CompanyFundamentals.FirstOrDefaultAsync(f => f.Symbol == symbol);

        if (cached != null && !forceRefresh && (DateTime.UtcNow - cached.FetchedAt) < CacheTtl)
            return cached;

        var provider = await _providers.GetFundamentalsProvider();
        if (provider == null)
        {
            _logger.LogWarning("Geen fundamentals provider beschikbaar");
            return cached; // geef stale cache terug als er geen provider is
        }

        try
        {
            // Haal alle data parallel op
            var metricsTask = provider.GetFinancialMetrics(symbol);
            var ratingsTask = provider.GetAnalystRatings(symbol);
            var targetTask  = provider.GetPriceTarget(symbol);

            await Task.WhenAll(metricsTask, ratingsTask, targetTask);

            var metrics = metricsTask.Result;
            var ratings = ratingsTask.Result;
            var target  = targetTask.Result;

            if (metrics == null && ratings == null && target == null)
                return cached;

            if (cached == null)
            {
                cached = new CompanyFundamentalsEntity { Symbol = symbol };
                _db.CompanyFundamentals.Add(cached);
            }

            // Update metrics
            if (metrics != null)
            {
                cached.PeRatio           = metrics.PeRatio;
                cached.ForwardPe         = metrics.ForwardPe;
                cached.PbRatio           = metrics.PbRatio;
                cached.PsRatio           = metrics.PsRatio;
                cached.EvToEbitda        = metrics.EvToEbitda;
                cached.ProfitMargin      = metrics.ProfitMargin;
                cached.OperatingMargin   = metrics.OperatingMargin;
                cached.ReturnOnEquity    = metrics.ReturnOnEquity;
                cached.ReturnOnAssets    = metrics.ReturnOnAssets;
                cached.RevenueGrowthYoy  = metrics.RevenueGrowthYoy;
                cached.EarningsGrowthYoy = metrics.EarningsGrowthYoy;
                cached.DebtToEquity      = metrics.DebtToEquity;
                cached.CurrentRatio      = metrics.CurrentRatio;
                cached.QuickRatio        = metrics.QuickRatio;
                cached.DividendYield     = metrics.DividendYield;
                cached.PayoutRatio       = metrics.PayoutRatio;
                cached.MarketCap         = metrics.MarketCap;
                cached.Revenue           = metrics.Revenue;
                cached.NetIncome         = metrics.NetIncome;
                cached.SharesOutstanding = metrics.SharesOutstanding;
            }

            // Update analyst data
            if (ratings != null)
            {
                cached.AnalystBuy        = ratings.Buy;
                cached.AnalystHold       = ratings.Hold;
                cached.AnalystSell       = ratings.Sell;
                cached.AnalystStrongBuy  = ratings.StrongBuy;
                cached.AnalystStrongSell = ratings.StrongSell;
            }

            if (target != null)
            {
                cached.TargetPriceHigh   = target.TargetHigh;
                cached.TargetPriceLow    = target.TargetLow;
                cached.TargetPriceMean   = target.TargetMean;
                cached.TargetPriceMedian = target.TargetMedian;
            }

            cached.FetchedAt = DateTime.UtcNow;
            cached.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Fundamentals bijgewerkt voor {Symbol}", symbol);
            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fundamentals ophalen mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return cached;
        }
    }

    /// <summary>
    /// Haalt insider transactions op. Slaat nieuwe transacties op, deduplicatie via unique constraint.
    /// </summary>
    public async Task<InsiderTransactionEntity[]> GetInsiderTransactions(string symbol, bool forceRefresh = false)
    {
        var cached = await _db.InsiderTransactions
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var mostRecent = cached.FirstOrDefault()?.FetchedAt ?? DateTime.MinValue;
        if (cached.Any() && !forceRefresh && (DateTime.UtcNow - mostRecent) < CacheTtl)
            return cached.ToArray();

        var provider = await _providers.GetFundamentalsProvider();
        if (provider == null) return cached.ToArray();

        try
        {
            var transactions = await provider.GetInsiderTransactions(symbol, months: 6);

            foreach (var tx in transactions)
            {
                var exists = await _db.InsiderTransactions.AnyAsync(t =>
                    t.Symbol == symbol && t.Name == tx.Name && t.TransactionDate == tx.Date);

                if (!exists)
                {
                    _db.InsiderTransactions.Add(new InsiderTransactionEntity
                    {
                        Symbol          = symbol,
                        Name            = tx.Name,
                        Relation        = tx.Relation,
                        TransactionType = tx.TransactionType,
                        TransactionDate = tx.Date,
                        Shares          = tx.Shares,
                        PricePerShare   = tx.PricePerShare,
                        TotalValue      = tx.TotalValue,
                        FetchedAt       = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("{Count} insider transacties opgeslagen voor {Symbol}", transactions.Length, symbol);

            return await _db.InsiderTransactions
                .Where(t => t.Symbol == symbol)
                .OrderByDescending(t => t.TransactionDate)
                .ToArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Insider transactions mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return cached.ToArray();
        }
    }

    /// <summary>
    /// Vernieuwt fundamentals voor alle actieve watchlist items.
    /// Rate-limited: 2 seconden pauze tussen symbolen.
    /// </summary>
    public async Task<int> RefreshAllWatchlistFundamentals()
    {
        var symbols = await _db.Watchlist
            .Where(w => w.IsActive)
            .Select(w => w.Symbol)
            .ToListAsync();

        int refreshed = 0;
        foreach (var symbol in symbols)
        {
            var result = await GetFundamentals(symbol, forceRefresh: true);
            if (result != null) refreshed++;
            await Task.Delay(2000); // rate limiting
        }
        return refreshed;
    }
}
```

---

## Deel 7: API Controller

**Nieuw bestand:** `src/AxonStockAgent.Api/Controllers/FundamentalsController.cs`

```csharp
using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/fundamentals")]
[Authorize]
public class FundamentalsController : ControllerBase
{
    private readonly FundamentalsService _fundamentals;

    public FundamentalsController(FundamentalsService fundamentals)
    {
        _fundamentals = fundamentals;
    }

    /// <summary>Haal alle fundamentals op voor een symbool (cached, 24h TTL).</summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetFundamentals(string symbol, [FromQuery] bool refresh = false)
    {
        var data = await _fundamentals.GetFundamentals(symbol.ToUpperInvariant(), refresh);
        if (data == null) return NotFound(new { error = $"Geen fundamentals gevonden voor {symbol}" });
        return Ok(new { data });
    }

    /// <summary>Haal insider transactions op voor een symbool.</summary>
    [HttpGet("{symbol}/insiders")]
    public async Task<IActionResult> GetInsiders(string symbol, [FromQuery] bool refresh = false)
    {
        var data = await _fundamentals.GetInsiderTransactions(symbol.ToUpperInvariant(), refresh);
        return Ok(new { data });
    }

    /// <summary>Admin: vernieuw fundamentals voor alle watchlist items.</summary>
    [HttpPost("refresh-all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RefreshAll()
    {
        var count = await _fundamentals.RefreshAllWatchlistFundamentals();
        return Ok(new { data = new { refreshed = count } });
    }
}
```

---

## Deel 8: Service Registratie

**Bestand:** `src/AxonStockAgent.Api/Program.cs`

Voeg toe bij de services (na `AlgoSettingsService`):

```csharp
builder.Services.AddScoped<FundamentalsService>();
```

---

## Deel 9: EF Core Migration

Genereer een migration:

```bash
cd src
dotnet ef migrations add AddCompanyFundamentalsAndInsiderTransactions --project AxonStockAgent.Api
```

---

## Deel 10: Frontend Types

**Bestand:** `frontend/src/types/index.ts`

Voeg toe:

```typescript
// Company Fundamentals
export interface CompanyFundamentals {
  id: number;
  symbol: string;
  // Valuation
  peRatio: number | null;
  forwardPe: number | null;
  pbRatio: number | null;
  psRatio: number | null;
  evToEbitda: number | null;
  // Profitability
  profitMargin: number | null;
  operatingMargin: number | null;
  returnOnEquity: number | null;
  returnOnAssets: number | null;
  // Growth
  revenueGrowthYoy: number | null;
  earningsGrowthYoy: number | null;
  // Balance sheet
  debtToEquity: number | null;
  currentRatio: number | null;
  quickRatio: number | null;
  // Dividends
  dividendYield: number | null;
  payoutRatio: number | null;
  // Size
  marketCap: number | null;
  revenue: number | null;
  netIncome: number | null;
  sharesOutstanding: number | null;
  // Analyst
  analystBuy: number | null;
  analystHold: number | null;
  analystSell: number | null;
  analystStrongBuy: number | null;
  analystStrongSell: number | null;
  targetPriceHigh: number | null;
  targetPriceLow: number | null;
  targetPriceMean: number | null;
  targetPriceMedian: number | null;
  // Meta
  fetchedAt: string;
  updatedAt: string;
}

export interface InsiderTransaction {
  id: number;
  symbol: string;
  name: string;
  relation: string;
  transactionType: string;
  transactionDate: string;
  shares: number;
  pricePerShare: number;
  totalValue: number;
  fetchedAt: string;
}
```

---

## Deel 11: Frontend Hooks

**Bestand:** `frontend/src/hooks/useApi.ts`

Voeg toe (importeer de nieuwe types):

```typescript
import type { ..., CompanyFundamentals, InsiderTransaction } from '../types';

// Fundamentals
export function useFundamentals(symbol: string) {
  return useQuery({
    queryKey: ['fundamentals', symbol],
    queryFn: () => api.get<ApiResponse<CompanyFundamentals>>(`/v1/fundamentals/${symbol}`),
    enabled: !!symbol,
    staleTime: 60 * 60 * 1000, // 1 uur client-side cache
  });
}

export function useInsiderTransactions(symbol: string) {
  return useQuery({
    queryKey: ['insiders', symbol],
    queryFn: () => api.get<ApiResponse<InsiderTransaction[]>>(`/v1/fundamentals/${symbol}/insiders`),
    enabled: !!symbol,
    staleTime: 60 * 60 * 1000,
  });
}

export function useRefreshAllFundamentals() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post('/v1/fundamentals/refresh-all', {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['fundamentals'] }),
  });
}
```

---

## Deel 12: Stock Detail Pagina

**Nieuw bestand:** `frontend/src/pages/StockDetailPage.tsx`

Maak een pagina die alle informatie over één aandeel toont. De route wordt `/stock/:symbol`.

De pagina moet de volgende secties bevatten:

### Header
- Symbool + bedrijfsnaam + logo (uit watchlist data)
- Sector badge + industry + country vlag
- Laatst bijgewerkt timestamp
- Refresh knop

### Valuation kaarten (grid van 5)
- P/E Ratio, Forward P/E, P/B, P/S, EV/EBITDA
- Elke kaart: label, waarde, kleur-indicator (groen = aantrekkelijk, rood = duur)
- P/E < 15 = groen, 15-25 = geel, > 25 = rood. Vergelijkbare logica voor andere metrics.

### Profitability & Growth (grid van 6)
- Profit Margin, Operating Margin, ROE, ROA, Revenue Growth, Earnings Growth
- Waarden als percentages formatteren (×100 + %)
- Positief = groen, negatief = rood

### Balance Sheet Health (grid van 3)
- Debt/Equity, Current Ratio, Quick Ratio
- D/E < 1 = groen, 1-2 = geel, > 2 = rood
- Current/Quick > 1.5 = groen, 1-1.5 = geel, < 1 = rood

### Dividend Info (grid van 2)
- Dividend Yield, Payout Ratio
- Yield als percentage, Payout < 60% = groen, 60-80% = geel, > 80% = roed

### Analyst Consensus
- Horizontale stacked bar: StrongBuy | Buy | Hold | Sell | StrongSell
- Kleuren: donkergroen, groen, geel, oranje, rood
- Price target: Low — Mean/Median — High als een range indicator
- Toon het getal bij elk segment

### Insider Trading tabel
- Tabel met kolommen: Datum, Naam, Rol, Type (buy/sell badge), Aandelen, Prijs, Totaal
- Buy transacties: groene badge, Sell: rode badge
- Sorteer op datum (nieuwste eerst)
- Max 20 rijen, "Toon meer" knop

### Stijl
- Gebruik hetzelfde dark theme als de rest van de app (bg-gray-950, border-gray-800, etc.)
- Gebruik de axon-kleuren voor accenten
- Responsive: 1 kolom op mobiel, 2-3 kolommen op desktop
- Loading skeletons tijdens data ophalen
- Error states als data niet beschikbaar

---

## Deel 13: Routing

**Bestand:** `frontend/src/App.tsx`

Voeg de route toe:

```tsx
import StockDetailPage from './pages/StockDetailPage';

// Binnen de protected routes, na de bestaande routes:
<Route path="/stock/:symbol" element={<StockDetailPage />} />
```

---

## Deel 14: Watchlist Koppeling

**Bestand:** `frontend/src/pages/WatchlistPage.tsx`

Maak elk symbool in de watchlist klikbaar. Wrap de symbool-tekst in een `<Link to={`/stock/${item.symbol}`}>` van react-router-dom. Stijl: `text-axon-400 hover:text-axon-300 cursor-pointer`.

---

## Verificatie Checklist

Na alle stappen, controleer:

- [ ] `dotnet build` in `src/` slaagt zonder errors
- [ ] EF migration bestand aanwezig
- [ ] `IFundamentalsProvider` heeft 5 methodes (GetProfile, GetAnalystRatings, GetFinancialMetrics, GetInsiderTransactions, GetPriceTarget)
- [ ] `FinnhubProvider` implementeert alle 5 methodes
- [ ] `FundamentalsService` is geregistreerd in Program.cs
- [ ] `FundamentalsController` heeft 3 endpoints: GET /{symbol}, GET /{symbol}/insiders, POST /refresh-all
- [ ] Frontend: types, hooks, StockDetailPage, route, watchlist links
- [ ] Geen TypeScript errors (`npm run build` in `frontend/`)
- [ ] Database init.sql bevat beide nieuwe tabellen

## Commit message

```
feat: add company fundamentals, analyst ratings & insider trading (#6)

- Extend IFundamentalsProvider with GetFinancialMetrics, GetInsiderTransactions, GetPriceTarget
- Implement all 3 methods in FinnhubProvider
- Add CompanyFundamentalsEntity and InsiderTransactionEntity
- FundamentalsService with 24h cache TTL and bulk refresh
- FundamentalsController: GET /fundamentals/{symbol}, GET /fundamentals/{symbol}/insiders, POST /fundamentals/refresh-all
- StockDetailPage: valuation, profitability, growth, balance sheet, dividends, analyst consensus, insider trades
- Watchlist symbols are now clickable links to stock detail
- Database tables: company_fundamentals, insider_transactions
```
