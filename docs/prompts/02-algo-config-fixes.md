# Prompt 02 — Algo Config Review Fixes

**Branch:** `feature/algo-config`  
**Doel:** Fix 6 review-bevindingen voordat we mergen naar main  
**Instructie:** Lees dit bestand volledig, voer ALLE taken uit, commit op dezelfde branch.

---

## Context

De `feature/algo-config` branch bevat een configureerbaar algoritme-instellingensysteem (AlgoSettingsEntity, AlgoSettingsService, Admin API endpoints, AdminSettingsPage frontend). Na review zijn er 6 verbeterpunten gevonden. Fix ze allemaal.

---

## Fix 1: Backend weights validatie

**Bestand:** `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs`

Voeg validatie toe aan de `Set()` methode voor de `weights` key. De vier gewichten (technical, ml, sentiment, claude) moeten optellen tot 1.0 (met een marge van 0.01). Als de validatie faalt, gooi een `ArgumentException`.

```csharp
public async Task Set(string key, JsonElement value)
{
    var json = value.GetRawText();
    
    // Validatie voor weights: moeten optellen tot 1.0
    if (key == "weights")
    {
        var weights = JsonSerializer.Deserialize<WeightsConfig>(json, _jsonOptions);
        if (weights != null)
        {
            var sum = weights.Technical + weights.Ml + weights.Sentiment + weights.Claude;
            if (Math.Abs(sum - 1.0) > 0.01)
                throw new ArgumentException($"Weights moeten optellen tot 1.0, huidige som: {sum:F2}");
            if (weights.Technical < 0 || weights.Ml < 0 || weights.Sentiment < 0 || weights.Claude < 0)
                throw new ArgumentException("Weights mogen niet negatief zijn");
        }
    }
    
    // Validatie voor thresholds
    if (key == "thresholds")
    {
        var thresholds = JsonSerializer.Deserialize<ThresholdsConfig>(json, _jsonOptions);
        if (thresholds != null)
        {
            if (thresholds.Bull <= 0 || thresholds.Bull > 1)
                throw new ArgumentException("Bull drempel moet tussen 0 en 1 liggen");
            if (thresholds.Bear >= 0 || thresholds.Bear < -1)
                throw new ArgumentException("Bear drempel moet tussen -1 en 0 liggen");
        }
    }
    
    // ... rest van de bestaande logica
}
```

Voeg ook error handling toe in de `AdminController.UpdateSetting` methode:

```csharp
[HttpPut("settings/{key}")]
public async Task<IActionResult> UpdateSetting(string key, [FromBody] JsonElement value)
{
    try
    {
        await _algoSettings.Set(key, value);
        return Ok();
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

---

## Fix 2: JsonSerializerOptions hergebruiken

**Bestand:** `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs`

Maak een static readonly field aan in de class in plaats van elke keer een nieuwe instance:

```csharp
public class AlgoSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AlgoSettingsService> _logger;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    // ... rest van de class
}
```

Vervang dan ALLE `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` door `_jsonOptions` in de methodes GetWeights, GetThresholds, GetTechnicalWeights, GetScanConfig, en GetFeatureFlags.

---

## Fix 3: Missende DbSets toevoegen

**Bestand:** `src/AxonStockAgent.Api/Data/AppDbContext.cs`

Voeg de ontbrekende DbSet properties toe voor NewsArticleEntity en SectorSentimentEntity:

```csharp
public DbSet<NewsArticleEntity> NewsArticles => Set<NewsArticleEntity>();
public DbSet<SectorSentimentEntity> SectorSentiment => Set<SectorSentimentEntity>();
```

Plaats ze na de bestaande DbSet-declaraties, vóór `AlgoSettings`.

---

## Fix 4: TypeScript interfaces voor settings

**Bestand:** `frontend/src/types/index.ts`

Voeg deze interfaces toe (onderaan het bestand):

```typescript
// Algo Settings types
export interface WeightsConfig {
  technical: number;
  ml: number;
  sentiment: number;
  claude: number;
}

export interface TechnicalWeightsConfig {
  trend: number;
  momentum: number;
  volatility: number;
  volume: number;
}

export interface ThresholdsConfig {
  bull: number;
  bear: number;
}

export interface ScanConfig {
  intervalMinutes: number;
  cooldownMinutes: number;
  candleHistory: number;
  timeframe: string;
}

export interface FeatureFlagsConfig {
  enableMl: boolean;
  enableClaude: boolean;
  enableSentiment: boolean;
  enableNewsFetcher: boolean;
}

export interface AlgoSettingsResponse {
  weights?: WeightsConfig;
  technical_weights?: TechnicalWeightsConfig;
  thresholds?: ThresholdsConfig;
  scan?: ScanConfig;
  features?: FeatureFlagsConfig;
}
```

**Bestand:** `frontend/src/hooks/useApi.ts`

Update de `useAlgoSettings` hook om het type te gebruiken:

```typescript
import type { ..., AlgoSettingsResponse } from '../types';

export function useAlgoSettings() {
  return useQuery({
    queryKey: ['algo-settings'],
    queryFn: () => api.get<AlgoSettingsResponse>('/v1/admin/settings'),
  });
}
```

**Bestand:** `frontend/src/pages/AdminSettingsPage.tsx`

Vervang alle `as any` casts door de juiste types. Import de types:

```typescript
import type { WeightsConfig, TechnicalWeightsConfig, ThresholdsConfig, ScanConfig, FeatureFlagsConfig } from '../types';
```

En update de useEffect:

```typescript
useEffect(() => {
  if (!settings || Object.keys(settings).length === 0) return;
  if (settings.weights) setWeights(settings.weights);
  if (settings.technical_weights) setTechWeights(settings.technical_weights);
  if (settings.thresholds) setThresholds(settings.thresholds);
  if (settings.scan) setScan(settings.scan);
  if (settings.features) setFeatures(settings.features);
}, [settings]);
```

Verwijder ALLE `as any` casts. Er mogen geen `as any` meer voorkomen in dit bestand.

---

## Fix 5: Error toast bij validatiefout

**Bestand:** `frontend/src/pages/AdminSettingsPage.tsx`

Voeg een error state toe en toon een foutmelding als de backend een validatiefout teruggeeft:

```typescript
const [error, setError] = useState<string | null>(null);

async function saveKey(key: string, value: unknown) {
  try {
    setError(null);
    await updateSetting.mutateAsync({ key, value });
    setSaved(key);
    setTimeout(() => setSaved(null), 2000);
  } catch (err: any) {
    const message = err?.response?.data?.error || err?.message || 'Opslaan mislukt';
    setError(message);
    setTimeout(() => setError(null), 5000);
  }
}
```

Toon de error bovenaan de pagina (onder de header):

```tsx
{error && (
  <div className="bg-red-900/50 border border-red-700 text-red-300 px-4 py-3 rounded-lg text-sm">
    {error}
  </div>
)}
```

---

## Fix 6: EF Core migration genereren

Dit is de belangrijkste fix. De `algo_settings` tabel bestaat wel in `init.sql` maar er is geen EF migration voor.

**Stappen:**

1. Zorg dat je in de `src/` directory staat
2. Run: `dotnet ef migrations add AddAlgoSettings --project AxonStockAgent.Api`
3. Controleer dat de gegenereerde migration een `CREATE TABLE algo_settings` bevat met kolommen: `Id`, `Key`, `Value`, `UpdatedAt`
4. Controleer dat er een unique index op `Key` wordt aangemaakt
5. Test dat `dotnet build` slaagt na de migration

Als `dotnet ef` niet beschikbaar is, installeer het eerst:
```bash
dotnet tool install --global dotnet-ef
```

---

## Verificatie checklist

Na alle fixes, controleer:

- [ ] `dotnet build` in `src/` slaagt zonder errors
- [ ] Geen `as any` meer in `AdminSettingsPage.tsx`
- [ ] `AlgoSettingsService` heeft één `_jsonOptions` static field
- [ ] `Set()` methode valideert weights en thresholds
- [ ] `AdminController.UpdateSetting` vangt `ArgumentException` af
- [ ] `AppDbContext` heeft DbSets voor NewsArticles en SectorSentiment
- [ ] TypeScript types voor alle settings config in `types/index.ts`
- [ ] EF migration bestand aanwezig in `Migrations/` folder
- [ ] Frontend toont error bij validatiefout

## Commit message

```
fix: address review findings for algo-config

- Add backend validation for weights (must sum to 1.0) and thresholds
- Reuse static JsonSerializerOptions instead of creating per-call
- Add missing DbSets for NewsArticles and SectorSentiment
- Add TypeScript interfaces, remove all 'as any' casts
- Add error handling/display for validation failures
- Generate EF Core migration for algo_settings table
```
