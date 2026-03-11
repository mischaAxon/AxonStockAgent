# Prompt 07: FundamentalsScorer — fundamentele analyse integreren in Worker scoring

## Context

In prompt 06 hebben we de score-normalisatie gefixt. Alle 5 bronnen tellen nu altijd mee, met 0.5 (neutraal) voor ontbrekende bronnen. Maar `fundNorm` is nog steeds hardcoded op `0.5`:

```csharp
var fundNorm = 0.5;  // TODO: vervangen door FundamentalsScorer in prompt 02
```

We hebben al:
- `CompanyFundamentalsEntity` in de database met P/E, ROE, margins, analist-ratings, price targets
- `FundamentalsService` die data ophaalt via providers en cached (24h TTL)
- `FinnhubProvider.GetFinancialMetrics()`, `GetAnalystRatings()`, `GetPriceTarget()`

Wat ontbreekt: een **scorer** die deze data omzet naar een 0-1 score, en de Worker die deze scorer aanroept.

## Wat moet er gebeuren

### Stap 1: FundamentalsScorer aanmaken

**Nieuw bestand: `src/AxonStockAgent.Core/Analysis/FundamentalsScorer.cs`**

Deze klasse is puur functioneel (static), net als `IndicatorEngine`. Hij accepteert de fundamentals data en de huidige prijs, en retourneert een score 0-1.

```csharp
namespace AxonStockAgent.Core.Analysis;

/// <summary>
/// Berekent een genormaliseerde fundamentele score (0-1) op basis van
/// waardering, winstgevendheid, groei, balans-gezondheid en analist-consensus.
/// 0.5 = neutraal, >0.5 = positief, <0.5 = negatief.
/// </summary>
public static class FundamentalsScorer
{
    /// <summary>
    /// Bereken de fundamentele score. Elke sub-score is optioneel;
    /// als een metric ontbreekt wordt die sub-score niet meegeteld.
    /// Retourneert 0.5 (neutraal) als er onvoldoende data is.
    /// </summary>
    public static FundamentalsResult Score(
        double? peRatio,
        double? forwardPe,
        double? pbRatio,
        double? profitMargin,
        double? operatingMargin,
        double? returnOnEquity,
        double? revenueGrowthYoy,
        double? earningsGrowthYoy,
        double? debtToEquity,
        double? currentRatio,
        int? analystBuy,
        int? analystHold,
        int? analystSell,
        int? analystStrongBuy,
        int? analystStrongSell,
        double? targetPriceMean,
        double currentPrice)
    {
        var components = new List<(double score, double weight, string name)>();

        // ── 1. Waardering (lagere P/E en P/B = beter, maar negatief = verliesgevend) ──
        var valScore = ScoreValuation(peRatio, forwardPe, pbRatio);
        if (valScore.HasValue)
            components.Add((valScore.Value, 0.25, "Valuation"));

        // ── 2. Winstgevendheid ──
        var profScore = ScoreProfitability(profitMargin, operatingMargin, returnOnEquity);
        if (profScore.HasValue)
            components.Add((profScore.Value, 0.25, "Profitability"));

        // ── 3. Groei ──
        var growthScore = ScoreGrowth(revenueGrowthYoy, earningsGrowthYoy);
        if (growthScore.HasValue)
            components.Add((growthScore.Value, 0.15, "Growth"));

        // ── 4. Balans-gezondheid ──
        var healthScore = ScoreFinancialHealth(debtToEquity, currentRatio);
        if (healthScore.HasValue)
            components.Add((healthScore.Value, 0.10, "Health"));

        // ── 5. Analist consensus ──
        var analystScore = ScoreAnalystConsensus(
            analystBuy, analystHold, analystSell,
            analystStrongBuy, analystStrongSell);
        if (analystScore.HasValue)
            components.Add((analystScore.Value, 0.15, "Analyst"));

        // ── 6. Price target upside/downside ──
        var targetScore = ScorePriceTarget(targetPriceMean, currentPrice);
        if (targetScore.HasValue)
            components.Add((targetScore.Value, 0.10, "PriceTarget"));

        // ── Gewogen gemiddelde ──
        if (components.Count == 0)
            return new FundamentalsResult(0.5, 0, "Geen data", Array.Empty<string>());

        var totalWeight = components.Sum(c => c.weight);
        var weightedScore = components.Sum(c => c.score * c.weight) / totalWeight;
        var finalScore = Clamp(weightedScore, 0, 1);

        var details = components
            .Select(c => $"{c.name}={c.score:F2}")
            .ToArray();

        var desc = finalScore switch
        {
            > 0.70 => "Strong Fundamentals",
            > 0.55 => "Good Fundamentals",
            > 0.45 => "Neutral Fundamentals",
            > 0.30 => "Weak Fundamentals",
            _ => "Poor Fundamentals"
        };

        return new FundamentalsResult(finalScore, components.Count, desc, details);
    }

    // ── Sub-scorers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Waardering: P/E en P/B. Lage waarden zijn beter (ondergewaardeerd),
    /// maar negatieve P/E = verliesgevend = slecht.
    /// Gebruikt een sigmoid-achtige mapping.
    /// </summary>
    private static double? ScoreValuation(double? pe, double? forwardPe, double? pb)
    {
        var scores = new List<double>();

        // P/E: ideaal 10-20, >40 is duur, <0 is verliesgevend
        var peVal = forwardPe ?? pe;  // Forward P/E heeft voorkeur
        if (peVal.HasValue)
        {
            if (peVal.Value < 0)
                scores.Add(0.2);  // verliesgevend
            else
                scores.Add(Clamp(1.0 - (peVal.Value - 15) / 50, 0.1, 0.9));
        }

        // P/B: ideaal 1-3, >5 is duur
        if (pb.HasValue && pb.Value > 0)
        {
            scores.Add(Clamp(1.0 - (pb.Value - 2) / 10, 0.1, 0.9));
        }

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Winstgevendheid: hogere margins en ROE zijn beter.
    /// </summary>
    private static double? ScoreProfitability(double? profitMargin, double? opMargin, double? roe)
    {
        var scores = new List<double>();

        // Profit margin: >20% is excellent, <0% is verliesgevend
        if (profitMargin.HasValue)
            scores.Add(Clamp(0.5 + profitMargin.Value / 40, 0.1, 0.9));

        // Operating margin: >25% is excellent
        if (opMargin.HasValue)
            scores.Add(Clamp(0.5 + opMargin.Value / 50, 0.1, 0.9));

        // ROE: >15% is goed, >25% is excellent
        if (roe.HasValue)
            scores.Add(Clamp(0.5 + roe.Value / 40, 0.1, 0.9));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Groei: positieve groei YoY is goed, negatief is slecht.
    /// </summary>
    private static double? ScoreGrowth(double? revenueGrowth, double? earningsGrowth)
    {
        var scores = new List<double>();

        // Revenue groei: >10% is goed, >25% is excellent
        if (revenueGrowth.HasValue)
            scores.Add(Clamp(0.5 + revenueGrowth.Value / 50, 0.15, 0.85));

        // Earnings groei: meer volatiel, bredere range
        if (earningsGrowth.HasValue)
            scores.Add(Clamp(0.5 + earningsGrowth.Value / 80, 0.15, 0.85));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Financiële gezondheid: lage schuld en gezonde current ratio.
    /// </summary>
    private static double? ScoreFinancialHealth(double? debtToEquity, double? currentRatio)
    {
        var scores = new List<double>();

        // D/E: <1 is gezond, >2 is risicovol
        if (debtToEquity.HasValue && debtToEquity.Value >= 0)
            scores.Add(Clamp(1.0 - debtToEquity.Value / 4, 0.1, 0.9));

        // Current ratio: >1.5 is gezond, <1 is risicovol
        if (currentRatio.HasValue && currentRatio.Value > 0)
            scores.Add(Clamp(currentRatio.Value / 3, 0.1, 0.9));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Analist consensus: meer buy/strongbuy vs sell/strongsell.
    /// </summary>
    private static double? ScoreAnalystConsensus(
        int? buy, int? hold, int? sell, int? strongBuy, int? strongSell)
    {
        var total = (buy ?? 0) + (hold ?? 0) + (sell ?? 0) + (strongBuy ?? 0) + (strongSell ?? 0);
        if (total == 0) return null;

        // Gewogen score: strongBuy=2, buy=1, hold=0, sell=-1, strongSell=-2
        var weighted = (strongBuy ?? 0) * 2.0 + (buy ?? 0) * 1.0
                     + (sell ?? 0) * -1.0 + (strongSell ?? 0) * -2.0;
        var maxPossible = total * 2.0;  // als iedereen strongBuy zou zeggen

        // Normaliseer naar 0-1
        return Clamp((weighted / maxPossible + 1) / 2, 0.1, 0.9);
    }

    /// <summary>
    /// Price target: hoeveel upside zien analisten?
    /// </summary>
    private static double? ScorePriceTarget(double? targetMean, double currentPrice)
    {
        if (!targetMean.HasValue || currentPrice <= 0 || targetMean.Value <= 0)
            return null;

        var upside = (targetMean.Value - currentPrice) / currentPrice;
        // +20% upside → 0.7, 0% → 0.5, -20% downside → 0.3
        return Clamp(0.5 + upside * 1.0, 0.15, 0.85);
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));
}

public record FundamentalsResult(
    double Score,
    int ComponentCount,
    string Description,
    string[] Details
);
```

### Stap 2: FundamentalsService registreren in Worker DI

**Bestand: `src/AxonStockAgent.Worker/Program.cs`**

De `FundamentalsService` is nog niet geregistreerd in de Worker. Voeg deze toe:

Zoek:
```csharp
builder.Services.AddScoped<NewsService>();
```

Voeg eronder toe:
```csharp
builder.Services.AddScoped<FundamentalsService>();
```

### Stap 3: ScreenerWorker aanpassen om fundamentals te scoren

**Bestand: `src/AxonStockAgent.Worker/ScreenerWorker.cs`**

#### 3a. FundamentalsService ophalen in RunScanCycleAsync

Zoek in `RunScanCycleAsync()`:
```csharp
var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
```

Voeg eronder toe:
```csharp
var fundamentalsService = scope.ServiceProvider.GetRequiredService<FundamentalsService>();
```

#### 3b. FundamentalsService doorgeven aan ScanSymbolAsync

In de aanroep van `ScanSymbolAsync` in de foreach-loop, voeg `fundamentalsService` toe als extra parameter. Voeg het toe vóór `normalizeMissingSources`.

Zoek:
```csharp
var signal = await ScanSymbolAsync(
    symbol, marketProvider, newsProviders, claudeService,
    techWeight, mlWeight, sentimentWeight, claudeWeight, fundamentalWeight,
    buyThreshold, sellThreshold, squeezeThreshold,
    lookbackDays, minVolume, normalizeMissingSources, ct);
```

Vervang door:
```csharp
var signal = await ScanSymbolAsync(
    symbol, marketProvider, newsProviders, claudeService, fundamentalsService,
    techWeight, mlWeight, sentimentWeight, claudeWeight, fundamentalWeight,
    buyThreshold, sellThreshold, squeezeThreshold,
    lookbackDays, minVolume, normalizeMissingSources, ct);
```

#### 3c. Method signature bijwerken

Zoek de method signature van `ScanSymbolAsync`:
```csharp
private async Task<AiEnrichedSignal?> ScanSymbolAsync(
    string symbol,
    IMarketDataProvider marketProvider,
    INewsProvider[] newsProviders,
    ClaudeAnalysisService claudeService,
    double techWeight, double mlWeight, double sentimentWeight,
```

Voeg `FundamentalsService fundamentalsService` toe na `ClaudeAnalysisService claudeService`:

```csharp
private async Task<AiEnrichedSignal?> ScanSymbolAsync(
    string symbol,
    IMarketDataProvider marketProvider,
    INewsProvider[] newsProviders,
    ClaudeAnalysisService claudeService,
    FundamentalsService fundamentalsService,
    double techWeight, double mlWeight, double sentimentWeight,
```

#### 3d. fundNorm berekenen met FundamentalsScorer

Dit is de kern. Zoek in `ScanSymbolAsync()` het blok:
```csharp
var fundNorm = 0.5;
```

Vervang dat door:

```csharp
// ── Fundamentele analyse ──
var fundNorm = 0.5;  // default neutraal
var fundDesc = "Geen data";
string[] fundDetails = Array.Empty<string>();
try
{
    var fundamentals = await fundamentalsService.GetFundamentals(symbol);
    if (fundamentals != null)
    {
        var currentPrice = candles[^1].Close;
        var fundResult = FundamentalsScorer.Score(
            peRatio:            fundamentals.PeRatio,
            forwardPe:          fundamentals.ForwardPe,
            pbRatio:            fundamentals.PbRatio,
            profitMargin:       fundamentals.ProfitMargin,
            operatingMargin:    fundamentals.OperatingMargin,
            returnOnEquity:     fundamentals.ReturnOnEquity,
            revenueGrowthYoy:   fundamentals.RevenueGrowthYoy,
            earningsGrowthYoy:  fundamentals.EarningsGrowthYoy,
            debtToEquity:       fundamentals.DebtToEquity,
            currentRatio:       fundamentals.CurrentRatio,
            analystBuy:         fundamentals.AnalystBuy,
            analystHold:        fundamentals.AnalystHold,
            analystSell:        fundamentals.AnalystSell,
            analystStrongBuy:   fundamentals.AnalystStrongBuy,
            analystStrongSell:  fundamentals.AnalystStrongSell,
            targetPriceMean:    fundamentals.TargetPriceMean,
            currentPrice:       currentPrice);

        fundNorm = fundResult.Score;
        fundDesc = fundResult.Description;
        fundDetails = fundResult.Details;
        _logger.LogDebug("{Symbol} fundamentals: {Score:F2} ({Desc}) [{Details}]",
            symbol, fundNorm, fundDesc, string.Join(", ", fundDetails));
    }
}
catch (Exception ex)
{
    _logger.LogDebug(ex, "Fundamentals scoring mislukt voor {Symbol}, gebruik neutraal", symbol);
}
```

Belangrijk: voeg bovenaan het bestand `ScreenerWorker.cs` een extra using toe:

```csharp
using AxonStockAgent.Core.Analysis;
```

(Dit staat er mogelijk al voor `IndicatorEngine`, controleer even.)

#### 3e. fundPresent boolean toevoegen aan de logging

Zoek de `var fundPresent` of het debug log-blok. Als `fundPresent` nog niet bestaat als boolean, voeg toe bij de andere `*Present` booleans:

Zoek:
```csharp
var sentPresent   = sentimentScore != 0;
var claudePresent = claude != null;
var mlPresent     = mlProbability.HasValue;
```

Voeg eronder toe:
```csharp
var fundPresent   = fundNorm != 0.5;
```

Update ook de debug log om `fundPresent` te tonen. Zoek de `_logger.LogDebug` regel die de score breakdown logt en vervang `fund={Fund:F3}` door `fund={Fund:F3}({FundP})`:

Wijzig de log naar:
```csharp
_logger.LogDebug(
    "{Symbol} score breakdown: tech={Tech:F3}, sent={Sent:F3}({SentP}), claude={Claude:F3}({ClaudeP}), ml={Ml:F3}({MlP}), fund={Fund:F3}({FundP}) → final={Final:F3} [{Mode}]",
    symbol,
    techNorm,
    sentPresent   ? sentNorm   : 0.5, sentPresent   ? "aanwezig" : "neutraal",
    claudePresent ? claudeNorm : 0.5, claudePresent ? "aanwezig" : "neutraal",
    mlPresent     ? mlNorm     : 0.5, mlPresent     ? "aanwezig" : "neutraal",
    fundNorm, fundPresent ? "aanwezig" : "neutraal",
    finalScore,
    normalizeMissingSources ? "normalize" : "legacy");
```

## Verificatie

```bash
# 1. Build check
cd src && dotnet build AxonStockAgent.sln

# 2. Controleer dat FundamentalsScorer.cs correct aangemaakt is
ls -la AxonStockAgent.Core/Analysis/FundamentalsScorer.cs

# 3. Controleer dat er geen TypeScript wijzigingen nodig zijn (frontend ongewijzigd)
cd ../frontend && npx tsc --noEmit
```

## Samenvatting van wijzigingen

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Core/Analysis/FundamentalsScorer.cs` | **Nieuw** — scorer met 6 sub-componenten |
| `src/AxonStockAgent.Worker/Program.cs` | `FundamentalsService` toevoegen aan DI |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | `fundNorm` berekenen via FundamentalsScorer + logging |

## Ontwerpkeuzes

**Waarom een aparte `FundamentalsScorer` in Core (niet in Worker)?**
Omdat het puur logica is zonder dependencies. Net als `IndicatorEngine` is het testbaar en herbruikbaar. De Worker orchestreert (data ophalen + scorer aanroepen), de scorer rekent.

**Waarom sub-scores gemiddeld i.p.v. optellen?**
Elke sub-score is 0-1. Door te middelen met gewichten (en alleen beschikbare sub-scores mee te tellen) is de eindscore robuust als sommige metrics ontbreken. Dit is hetzelfde principe als de normalisatie in prompt 06.

**Waarom clamp op 0.1-0.9 per sub-score?**
Om te voorkomen dat één extreme metric (bijv. P/E van 200) de hele score naar 0 of 1 duwt. De schaal blijft genuanceerd.

**Waarom FundamentalsService.GetFundamentals() gebruiken i.p.v. direct de database?**
De service heeft caching met 24h TTL en fallback bij stale data. Als de Finnhub API faalt, krijg je nog steeds de laatste bekende data. Dit is gratis betrouwbaarheid.

**Impact op scores:**
- Symbolen met goede fundamentals (bijv. AAPL: hoge margins, analist buy-consensus) krijgen fundNorm > 0.5, wat de eindscore licht omhoog trekt
- Symbolen zonder fundamentals data (bijv. nieuwe watchlist items) houden fundNorm = 0.5 (neutraal), dankzij prompt 06
- Het gewicht is 10% — subtiele invloed, niet dominant
