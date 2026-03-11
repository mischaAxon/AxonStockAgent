# Prompt 06: Score-normalisatie fix + transparante gewichten

## Context

De `ScreenerWorker` berekent een gewogen eindscore uit 5 bronnen: technical, ML, sentiment, Claude, fundamental. Het probleem: **de gewichten worden dynamisch opgeteld op basis van welke bronnen data leverden**. Dit betekent dat de eindscore totaal verschilt per symbool:

- AAPL (alles beschikbaar): tech=0.30 + ML=0 + sent=0.20 + claude=0.15 + fund=0.10 → totalWeight=0.75
- ASML.AS (EU, sentiment=403): tech=0.30 + claude=0.15 + fund=0.10 → totalWeight=0.55

Dezelfde "kwaliteit" aandeel krijgt een **andere score** puur omdat er minder bronnen beschikbaar zijn. Dat maakt de BUY/SELL thresholds onbetrouwbaar.

Daarnaast: `fundNorm` is hardcoded op `0.5` (neutraal). Dat fixen we in een latere prompt, maar de normalisatie moet nu al correct zijn.

## Wat moet er gebeuren

### Stap 1: Nieuwe algo_setting toevoegen

Voeg een nieuwe setting toe aan de `scan` categorie die bepaalt hoe ontbrekende bronnen behandeld worden.

**Bestand: `database/init.sql`**

Voeg toe aan het `INSERT INTO algo_settings` blok, net na de bestaande scan-settings:

```sql
('scan', 'normalize_missing_sources', 'true', 'Bij ontbrekende databronnen (ML, sentiment, Claude): gebruik neutraal (0.5) i.p.v. overslaan. Zorgt voor consistente scores ongeacht beschikbare providers.', 'boolean', null, null),
('scan', 'signal_dedup_minutes', '60', 'Deduplicatie-window in minuten: voorkomt dubbele signalen voor hetzelfde symbool/verdict', 'integer', 5, 1440),
```

Let op: `signal_dedup_minutes` bestaat al als hardcoded default in de Worker maar staat nog niet in de seed data. Voeg deze ook toe zodat hij configureerbaar is via de admin UI.

**SQL om in bestaande database toe te voegen** (voor als `init.sql` niet opnieuw draait):

```sql
INSERT INTO algo_settings (category, key, value, description, value_type, min_value, max_value) VALUES
    ('scan', 'normalize_missing_sources', 'true', 'Bij ontbrekende databronnen (ML, sentiment, Claude): gebruik neutraal (0.5) i.p.v. overslaan. Zorgt voor consistente scores ongeacht beschikbare providers.', 'boolean', null, null),
    ('scan', 'signal_dedup_minutes', '60', 'Deduplicatie-window in minuten: voorkomt dubbele signalen voor hetzelfde symbool/verdict', 'integer', 5, 1440)
ON CONFLICT (category, key) DO NOTHING;
```

### Stap 2: AlgoSettingsService.ResetToDefaultsAsync() bijwerken

**Bestand: `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs`**

In de `ResetToDefaultsAsync()` methode staat een hardcoded seed SQL string. Voeg de twee nieuwe settings toe aan dat INSERT statement, direct na de bestaande scan-settings:

Zoek in de seed SQL string:
```sql
('scan', 'min_volume',                '100000','Minimum gemiddeld volume',               'integer', 0,   null),
```

Voeg daarachter toe (vóór de notifications-regels):
```sql
('scan', 'normalize_missing_sources', 'true', 'Bij ontbrekende databronnen: gebruik neutraal (0.5) i.p.v. overslaan', 'boolean', null, null),
('scan', 'signal_dedup_minutes',      '60',   'Deduplicatie-window in minuten voor signaal-dedup', 'integer', 5, 1440),
```

### Stap 3: ScreenerWorker scoring-logica herschrijven

**Bestand: `src/AxonStockAgent.Worker/ScreenerWorker.cs`**

Dit is de kern van de fix. In de methode `RunScanCycleAsync`, na het ophalen van de bestaande settings, haal ook de nieuwe setting op:

Zoek:
```csharp
var dedupWindowMinutes = (int)await algoSettings.GetDecimalAsync("scan", "signal_dedup_minutes", 60m);
```

Voeg daaronder toe:
```csharp
var normalizeMissingSources = await algoSettings.GetBoolAsync("scan", "normalize_missing_sources", true);
```

Geef deze waarde door als extra parameter aan `ScanSymbolAsync`. Voeg `bool normalizeMissingSources` toe als laatste parameter van de methode-signatuur, na `long minVolume`.

In de `foreach` loop waar `ScanSymbolAsync` aangeroepen wordt, voeg `normalizeMissingSources` toe als laatste argument.

**Nu het belangrijkste deel — de scoring-logica in `ScanSymbolAsync`:**

Zoek het hele blok dat begint met:
```csharp
// ── Gewogen eindscore berekenen ──
var techNorm = (techScore + 1) / 2;
```

En eindigt met:
```csharp
var finalScore = totalWeight > 0 ? weightedSum / totalWeight : 0.5;
```

Vervang dit volledige blok door:

```csharp
// ── Gewogen eindscore berekenen ──
// Normaliseer alle scores naar 0-1 bereik
var techNorm = (techScore + 1) / 2;                          // altijd beschikbaar
var sentNorm = sentimentScore != 0 ? (sentimentScore + 1) / 2 : 0.5;
var claudeNorm = 0.5;                                         // default neutraal
if (claude != null)
{
    claudeNorm = claude.Confidence;
    if (claude.Direction == "SELL") claudeNorm = 1 - claudeNorm;
}
var mlNorm = mlProbability.HasValue ? (double)mlProbability.Value : 0.5;
var fundNorm = 0.5;  // TODO: vervangen door FundamentalsScorer in prompt 02

double finalScore;
if (normalizeMissingSources)
{
    // Modus 1 (standaard): Alle gewichten tellen altijd mee.
    // Ontbrekende bronnen krijgen 0.5 (neutraal) — geen invloed op de score,
    // maar de verhouding tussen gewichten blijft constant.
    finalScore = techNorm      * techWeight
               + mlNorm        * mlWeight
               + sentNorm      * sentimentWeight
               + claudeNorm    * claudeWeight
               + fundNorm      * fundamentalWeight;
    // Gewichten optellen tot 1.0 (afgedwongen door admin validatie), dus geen deling nodig
}
else
{
    // Modus 2 (legacy): Alleen beschikbare bronnen tellen mee.
    // De effectieve gewichten verschuiven per symbool.
    double totalWeight = techWeight;
    double weightedSum = techNorm * techWeight;

    if (mlProbability.HasValue)  { totalWeight += mlWeight;        weightedSum += mlNorm * mlWeight; }
    if (sentimentScore != 0)     { totalWeight += sentimentWeight; weightedSum += sentNorm * sentimentWeight; }
    if (claude != null)          { totalWeight += claudeWeight;    weightedSum += claudeNorm * claudeWeight; }
    totalWeight += fundamentalWeight; weightedSum += fundNorm * fundamentalWeight;

    finalScore = totalWeight > 0 ? weightedSum / totalWeight : 0.5;
}
```

### Stap 4: Logging toevoegen voor transparantie

Nog steeds in `ScanSymbolAsync`, **na** de score-berekening en **vóór** het verdict-blok, voeg deze log toe:

Zoek:
```csharp
// ── Verdict bepalen ──
```

Voeg daarboven toe:
```csharp
// Log de score-componenten voor debugging/transparantie
_logger.LogDebug(
    "{Symbol}: scores tech={Tech:F2} ml={Ml:F2} sent={Sent:F2} claude={Claude:F2} fund={Fund:F2} → final={Final:F2} (normalize={Normalize})",
    symbol, techNorm, mlNorm, sentNorm, claudeNorm, fundNorm, finalScore, normalizeMissingSources);
```

### Stap 5: dedup_minutes uit settings lezen i.p.v. hardcoded

In `RunScanCycleAsync` wordt `dedupWindowMinutes` al uit de settings gelezen via `GetDecimalAsync`. Maar als de setting niet bestaat (oude database), valt hij terug op default 60. Dat is correct, geen wijziging nodig hier — maar door de seed data in stap 1 is hij nu ook configureerbaar via de admin UI.

## Verificatie

Na alle wijzigingen:

```bash
# 1. Build check
cd src && dotnet build

# 2. Controleer dat de solution compileert zonder errors
dotnet build AxonStockAgent.sln

# 3. TypeScript check (frontend niet gewijzigd, maar voor de zekerheid)
cd ../frontend && npx tsc --noEmit
```

## Samenvatting van wijzigingen

| Bestand | Actie |
|---------|-------|
| `database/init.sql` | Twee nieuwe `algo_settings` rijen toevoegen |
| `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` | Seed SQL in `ResetToDefaultsAsync()` uitbreiden |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | Score-normalisatie logica herschrijven + logging |

## Ontwerpkeuzes

**Waarom altijd 0.5 voor ontbrekende bronnen?**
0.5 is het middenpunt van het 0-1 bereik. Het trekt de score niet omhoog en niet omlaag. Dit is wiskundig equivalent aan "ik heb geen mening" — precies wat je wilt als een bron niet beschikbaar is.

**Waarom de legacy-modus bewaren?**
Zodat je via de admin UI kunt vergelijken. Zet `normalize_missing_sources` op `false` en je hebt het oude gedrag terug. Handig voor A/B testing van signaal-kwaliteit.

**Waarom geen deling meer in normalize-modus?**
De `ValidateWeightsSum()` in `AlgoSettingsService` dwingt al af dat gewichten optellen tot 1.0. Dus `tech*0.30 + ml*0.25 + sent*0.20 + claude*0.15 + fund*0.10 = gewogen gemiddelde` zonder deling.
