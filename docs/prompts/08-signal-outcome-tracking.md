# Prompt 08: Signal Outcome Tracking

## Context

Het systeem genereert BUY/SELL/SQUEEZE signalen, maar trackt niet wat er daarna gebeurt. Er is geen manier om te meten of signalen correct waren. Zonder outcome tracking is "betrouwbare signaleerder" een loze claim.

We voegen toe:
1. Outcome-velden op `SignalEntity` (prijs na 1d, 5d, 20d + return %)
2. Een achtergrond-job die deze velden vult
3. Een API endpoint om de signaal-nauwkeurigheid te bekijken
4. Frontend types bijwerken

## Wat moet er gebeuren

### Stap 1: SignalEntity uitbreiden

**Bestand: `src/AxonStockAgent.Api/Data/Entities/SignalEntity.cs`**

Voeg onderaan de klasse toe, na `public DateTime CreatedAt { get; set; }`:

```csharp
// Outcome tracking
public double? PriceAfter1d { get; set; }
public double? PriceAfter5d { get; set; }
public double? PriceAfter20d { get; set; }
public double? ReturnPct1d { get; set; }
public double? ReturnPct5d { get; set; }
public double? ReturnPct20d { get; set; }
public bool? OutcomeCorrect { get; set; }  // Was het signaal achteraf correct?
```

### Stap 2: Database schema uitbreiden

**Bestand: `database/init.sql`**

Voeg toe direct na het `CREATE TABLE IF NOT EXISTS signals` blok (na de kolom `created_at`), maar vóór de `CREATE INDEX` regels voor signals:

Eigenlijk: voeg ALTER TABLE statements toe aan het einde van het signals-gedeelte, net na de bestaande `CREATE INDEX` regels voor signals:

```sql
-- Outcome tracking kolommen
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_1d    DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_5d    DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS price_after_20d   DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_1d     DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_5d     DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS return_pct_20d    DOUBLE PRECISION;
ALTER TABLE signals ADD COLUMN IF NOT EXISTS outcome_correct   BOOLEAN;
```

Gebruik `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` zodat het idempotent is op bestaande databases.

### Stap 3: AppDbContext bijwerken

**Bestand: `src/AxonStockAgent.Api/Data/AppDbContext.cs`**

Geen wijziging nodig — EF Core pikt de nieuwe properties automatisch op via convention. De snake_case naming convention zet `PriceAfter1d` om naar `price_after_1d`.

### Stap 4: SignalOutcomeService aanmaken

**Nieuw bestand: `src/AxonStockAgent.Api/Services/SignalOutcomeService.cs`**

Deze service zoekt signalen waarvan de outcome nog niet ingevuld is, fetcht de huidige prijs, en vult de outcome-velden in.

```csharp
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Vult outcome-velden in voor bestaande signalen.
/// Kijkt hoeveel tradingdagen er verstreken zijn sinds het signaal
/// en haalt de huidige prijs op om return te berekenen.
/// </summary>
public class SignalOutcomeService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<SignalOutcomeService> _logger;

    public SignalOutcomeService(AppDbContext db, ProviderManager providers, ILogger<SignalOutcomeService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Verwerk alle signalen die outcome-updates nodig hebben.
    /// Een signaal heeft een update nodig als:
    /// - PriceAfter1d is null EN signaal is >= 1 tradingdag oud
    /// - PriceAfter5d is null EN signaal is >= 5 tradingdagen oud
    /// - PriceAfter20d is null EN signaal is >= 20 tradingdagen oud
    /// </summary>
    public async Task<int> ProcessOutcomesAsync(CancellationToken ct = default)
    {
        var provider = await _providers.GetMarketDataProvider();
        if (provider == null)
        {
            _logger.LogWarning("Geen market data provider beschikbaar voor outcome tracking");
            return 0;
        }

        // Haal signalen op die nog onvolledige outcomes hebben en oud genoeg zijn
        var cutoff1d  = DateTime.UtcNow.AddDays(-2);    // minstens 1 tradingdag
        var cutoff5d  = DateTime.UtcNow.AddDays(-8);    // minstens 5 tradingdagen (+ weekend)
        var cutoff20d = DateTime.UtcNow.AddDays(-30);   // minstens 20 tradingdagen (+ weekenden)

        var signals = await _db.Signals
            .Where(s =>
                (s.PriceAfter1d == null && s.CreatedAt <= cutoff1d) ||
                (s.PriceAfter5d == null && s.CreatedAt <= cutoff5d) ||
                (s.PriceAfter20d == null && s.CreatedAt <= cutoff20d))
            .OrderBy(s => s.CreatedAt)
            .Take(50)  // batch-limiet om API rate limits te respecteren
            .ToListAsync(ct);

        if (signals.Count == 0) return 0;

        // Groepeer per symbool om candles maar 1x per symbool op te halen
        var symbolGroups = signals.GroupBy(s => s.Symbol);
        int updated = 0;

        foreach (var group in symbolGroups)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var symbol = group.Key;
                var candles = await provider.GetCandles(symbol, "D", 60);
                if (candles == null || candles.Length < 2)
                {
                    _logger.LogDebug("Onvoldoende candles voor outcome tracking van {Symbol}", symbol);
                    continue;
                }

                foreach (var signal in group)
                {
                    updated += UpdateSignalOutcome(signal, candles) ? 1 : 0;
                }

                await Task.Delay(500, ct);  // rate limiting
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outcome tracking mislukt voor {Symbol}", group.Key);
            }
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Outcome tracking: {Updated} signalen bijgewerkt", updated);
        }

        return updated;
    }

    /// <summary>
    /// Vul de outcome-velden in voor één signaal op basis van candle data.
    /// Zoekt de candle die het dichtst bij de target-datum ligt.
    /// Retourneert true als er iets gewijzigd is.
    /// </summary>
    private bool UpdateSignalOutcome(SignalEntity signal, Core.Models.Candle[] candles)
    {
        bool changed = false;
        var signalDate = signal.CreatedAt;
        var basePrice = signal.PriceAtSignal;
        if (basePrice <= 0) return false;

        // 1 dag: zoek candle ~1 tradingdag na signaal
        if (signal.PriceAfter1d == null)
        {
            var targetDate = AddTradingDays(signalDate, 1);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter1d = price.Value;
                signal.ReturnPct1d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // 5 dagen
        if (signal.PriceAfter5d == null)
        {
            var targetDate = AddTradingDays(signalDate, 5);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter5d = price.Value;
                signal.ReturnPct5d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // 20 dagen
        if (signal.PriceAfter20d == null)
        {
            var targetDate = AddTradingDays(signalDate, 20);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter20d = price.Value;
                signal.ReturnPct20d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // Bepaal OutcomeCorrect op basis van de langste beschikbare return
        var longestReturn = signal.ReturnPct20d ?? signal.ReturnPct5d ?? signal.ReturnPct1d;
        if (longestReturn.HasValue)
        {
            signal.OutcomeCorrect = signal.FinalVerdict switch
            {
                "BUY" or "SQUEEZE" => longestReturn.Value > 0,
                "SELL" => longestReturn.Value < 0,
                _ => null
            };
        }

        return changed;
    }

    /// <summary>
    /// Voeg N tradingdagen toe (skip weekenden).
    /// </summary>
    private static DateTime AddTradingDays(DateTime start, int days)
    {
        var date = start;
        int added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                added++;
        }
        return date;
    }

    /// <summary>
    /// Zoek de close-prijs van de candle die het dichtst bij targetDate ligt.
    /// Retourneert null als er geen candle is na de targetDate (data nog niet beschikbaar).
    /// </summary>
    private static double? FindClosestPrice(Core.Models.Candle[] candles, DateTime targetDate)
    {
        // Zoek de eerste candle op of na de targetDate
        var candidate = candles
            .Where(c => c.Time >= targetDate.Date)
            .OrderBy(c => c.Time)
            .FirstOrDefault();

        if (candidate.Time == default) return null;

        // Accepteer candles tot 5 dagen na target (voor feestdagen etc.)
        if ((candidate.Time - targetDate).TotalDays > 5) return null;

        return candidate.Close;
    }
}
```

### Stap 5: OutcomeTrackerService (BackgroundService) aanmaken

**Nieuw bestand: `src/AxonStockAgent.Api/BackgroundServices/OutcomeTrackerService.cs`**

```csharp
using AxonStockAgent.Api.Services;

namespace AxonStockAgent.Api.BackgroundServices;

/// <summary>
/// Achtergrond-job die elke 6 uur signaal-outcomes bijwerkt.
/// Draait in de API-container (niet de Worker), omdat het lichtgewicht is
/// en alleen bestaande signalen + candle data nodig heeft.
/// </summary>
public class OutcomeTrackerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutcomeTrackerService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    public OutcomeTrackerService(IServiceScopeFactory scopeFactory, ILogger<OutcomeTrackerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outcome tracker gestart, interval: {Interval}", Interval);

        // Wacht even tot de database en providers klaar zijn
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var outcomeService = scope.ServiceProvider.GetRequiredService<SignalOutcomeService>();

                var updated = await outcomeService.ProcessOutcomesAsync(stoppingToken);
                if (updated > 0)
                    _logger.LogInformation("Outcome tracker: {Count} signalen bijgewerkt", updated);
                else
                    _logger.LogDebug("Outcome tracker: geen signalen om bij te werken");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outcome tracker cycle mislukt");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
```

### Stap 6: Services registreren in API Program.cs

**Bestand: `src/AxonStockAgent.Api/Program.cs`**

Zoek:
```csharp
builder.Services.AddHostedService<NewsFetcherService>();
```

Voeg eronder toe:
```csharp
builder.Services.AddScoped<SignalOutcomeService>();
builder.Services.AddHostedService<OutcomeTrackerService>();
```

Voeg ook de juiste using toe bovenaan als die er nog niet staat:
```csharp
using AxonStockAgent.Api.BackgroundServices;
```

### Stap 7: API endpoint voor signaal-nauwkeurigheid

**Bestand: `src/AxonStockAgent.Api/Controllers/SignalsController.cs`**

Voeg een nieuw endpoint toe aan het einde van de klasse, na `GetStats()`:

```csharp
[HttpGet("accuracy")]
public async Task<IActionResult> GetAccuracy([FromQuery] int days = 30)
{
    var since = DateTime.UtcNow.AddDays(-days);

    var signals = await _db.Signals
        .Where(s => s.CreatedAt >= since && s.OutcomeCorrect.HasValue)
        .ToListAsync();

    if (signals.Count == 0)
        return Ok(new { data = new { totalTracked = 0, message = "Nog geen outcome data beschikbaar" } });

    var correct = signals.Count(s => s.OutcomeCorrect == true);
    var incorrect = signals.Count(s => s.OutcomeCorrect == false);
    var accuracy = signals.Count > 0 ? (double)correct / signals.Count * 100 : 0;

    var byVerdict = signals
        .GroupBy(s => s.FinalVerdict)
        .Select(g => new
        {
            verdict = g.Key,
            total = g.Count(),
            correct = g.Count(s => s.OutcomeCorrect == true),
            accuracy = g.Count() > 0 ? (double)g.Count(s => s.OutcomeCorrect == true) / g.Count() * 100 : 0,
            avgReturn1d = g.Where(s => s.ReturnPct1d.HasValue).Select(s => s.ReturnPct1d!.Value).DefaultIfEmpty(0).Average(),
            avgReturn5d = g.Where(s => s.ReturnPct5d.HasValue).Select(s => s.ReturnPct5d!.Value).DefaultIfEmpty(0).Average(),
            avgReturn20d = g.Where(s => s.ReturnPct20d.HasValue).Select(s => s.ReturnPct20d!.Value).DefaultIfEmpty(0).Average()
        })
        .ToList();

    return Ok(new
    {
        data = new
        {
            totalTracked = signals.Count,
            correct,
            incorrect,
            accuracyPct = Math.Round(accuracy, 1),
            byVerdict,
            periodDays = days
        }
    });
}
```

### Stap 8: Frontend types bijwerken

**Bestand: `frontend/src/types/index.ts`**

Voeg de outcome-velden toe aan de `Signal` interface. Zoek:
```typescript
notified: boolean;
createdAt: string;
```

Voeg eronder toe:
```typescript
// Outcome tracking
priceAfter1d: number | null;
priceAfter5d: number | null;
priceAfter20d: number | null;
returnPct1d: number | null;
returnPct5d: number | null;
returnPct20d: number | null;
outcomeCorrect: boolean | null;
```

## Verificatie

```bash
# 1. Build check
cd src && dotnet build AxonStockAgent.sln

# 2. Controleer nieuwe bestanden
ls -la AxonStockAgent.Api/Services/SignalOutcomeService.cs
ls -la AxonStockAgent.Api/BackgroundServices/OutcomeTrackerService.cs

# 3. TypeScript check
cd ../frontend && npx tsc --noEmit

# 4. Na deploy: controleer dat de kolommen bestaan
# docker compose exec db psql -U postgres -d axonstockagent -c "\d signals"

# 5. Na een paar dagen: test het accuracy endpoint
# curl -H "Authorization: Bearer <token>" http://localhost:5000/api/v1/signals/accuracy?days=30
```

## Samenvatting van wijzigingen

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Api/Data/Entities/SignalEntity.cs` | 7 nieuwe outcome properties |
| `database/init.sql` | ALTER TABLE voor outcome kolommen |
| `src/AxonStockAgent.Api/Services/SignalOutcomeService.cs` | **Nieuw** — outcome-berekening |
| `src/AxonStockAgent.Api/BackgroundServices/OutcomeTrackerService.cs` | **Nieuw** — achtergrond-job (elke 6 uur) |
| `src/AxonStockAgent.Api/Program.cs` | DI registraties |
| `src/AxonStockAgent.Api/Controllers/SignalsController.cs` | `/accuracy` endpoint |
| `frontend/src/types/index.ts` | Signal type uitbreiden |

## Ontwerpkeuzes

**Waarom in de API-container en niet de Worker?**
De Worker scant symbolen en genereert signalen. Outcome tracking is een ander concern: het leest bestaande signalen en fetcht prijzen achteraf. Door het in de API-container te draaien houd je de Worker gefocust en kun je de API onafhankelijk deployen.

**Waarom tradingdagen i.p.v. kalenderdagen?**
Een signaal op vrijdag zou anders zaterdag als "1d" tellen, terwijl de beurs dicht is. `AddTradingDays()` skipt weekenden. Feestdagen worden afgevangen door `FindClosestPrice()` die tot 5 dagen na de target accepteert.

**Waarom batch-limiet van 50?**
Bij 7 symbolen en 3 tijdstippen per signaal zijn dat potentieel veel API calls. De batch-limiet van 50 signalen en de 500ms rate limiting zorgen ervoor dat we niet tegen Finnhub's rate limit aanlopen. Bij de volgende cycle (6 uur later) worden de rest verwerkt.

**Waarom `OutcomeCorrect` op de langste beschikbare return?**
Een signaal is "correct" als de prijs na 20d in de verwachte richting bewoog. Als 20d nog niet beschikbaar is, gebruiken we 5d, anders 1d. Dit wordt automatisch bijgewerkt naarmate er meer data beschikbaar komt.

**Wat is "correct"?**
- BUY/SQUEEZE signaal + positieve return = correct
- SELL signaal + negatieve return = correct
- Dit is simplistisch (geen risk-adjusted returns), maar het is een goede eerste stap om de signaal-kwaliteit te meten.
