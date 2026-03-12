# Prompt 21 — Index Import via Finnhub (US) + Claude AI (NL/overig)

## Doel

Vervang de EODHD fundamentals-afhankelijkheid voor index-componenten door:
1. **Finnhub** (gratis) voor US-indexen: S&P 500, NASDAQ-100, Dow Jones 30
2. **Claude API** voor NL-indexen en overige: vraag Claude om de huidige samenstelling te retourneren als JSON

De admin UI krijgt per index twee import-opties:
- **"API Import"** — probeert Finnhub (voor US) of EODHD (als beschikbaar)
- **"AI Import"** — vraagt Claude API om de componenten (werkt voor alle indexen)

## Verificatie

```bash
cd src/AxonStockAgent.Api && dotnet build --nologo -v quiet
cd frontend && npx tsc --noEmit && npm run build
```

---

## Stap 1: Voeg `GetIndexConstituents` toe aan `FinnhubProvider`

Open `src/AxonStockAgent.Api/Providers/FinnhubProvider.cs`.

Voeg deze methode toe (naast de bestaande methodes):

```csharp
/// <summary>
/// Haal index-componenten op via Finnhub gratis API.
/// Ondersteunt: ^GSPC (S&P 500), ^NDX (NASDAQ-100), ^DJI (Dow Jones 30) en meer.
/// Retourneert een array van ticker symbols (bijv. ["AAPL", "MSFT", ...]).
/// </summary>
public async Task<string[]> GetIndexConstituents(string finnhubSymbol)
{
    await RateLimit();
    var url = $"{BaseUrl}/index/constituents?symbol={Uri.EscapeDataString(finnhubSymbol)}&token={_apiKey}";
    try
    {
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("constituents", out var constituents))
            return Array.Empty<string>();

        return constituents.EnumerateArray()
            .Select(c => c.GetString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
    {
        _logger.LogDebug("Finnhub: index constituents voor {Symbol} niet beschikbaar (403)", finnhubSymbol);
        return Array.Empty<string>();
    }
    catch (Exception ex)
    {
        _logger.LogWarning("Finnhub GetIndexConstituents mislukt voor {Symbol}: {Message}", finnhubSymbol, ex.Message);
        return Array.Empty<string>();
    }
}
```

---

## Stap 2: Maak `ClaudeIndexService` in de API

De bestaande `ClaudeAnalysisService` leeft in het Worker project. We maken een nieuwe lichte service in het API project die Claude kan aanroepen voor index-componenten.

Maak `src/AxonStockAgent.Api/Services/ClaudeIndexService.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Vraagt Claude AI om de huidige samenstelling van een beursindex te retourneren.
/// Gebruikt voor indexen waar geen gratis API beschikbaar is (bijv. AEX, AMX, AScX).
/// </summary>
public class ClaudeIndexService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ClaudeIndexService> _logger;

    private const string Model = "claude-sonnet-4-20250514";

    public ClaudeIndexService(HttpClient http, IConfiguration config, ILogger<ClaudeIndexService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Vraag Claude om de componenten van een index.
    /// Retourneert een lijst van (Code, Name, Sector) tuples.
    /// </summary>
    public async Task<IndexComponentResult[]> GetIndexComponentsViaAI(string indexName, string exchangeCode)
    {
        var apiKey = _config["Claude:ApiKey"] ?? _config["ANTHROPIC_API_KEY"] ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Claude API key niet geconfigureerd");
            return Array.Empty<IndexComponentResult>();
        }

        var prompt = $$$"""
            Je bent een financieel data-expert. Geef de HUIDIGE samenstelling van de {{{indexName}}} index.

            Retourneer UITSLUITEND een JSON array (geen markdown, geen toelichting). Elk element bevat:
            - "code": het ticker-symbool zoals het op de beurs wordt verhandeld (bijv. "ASML" voor Euronext Amsterdam, "AAPL" voor US)
            - "name": de volledige bedrijfsnaam
            - "sector": de GICS-sector (bijv. "Technology", "Healthcare", "Financials")

            De exchange code is "{{{exchangeCode}}}".

            Voorbeeld formaat:
            [{"code":"ASML","name":"ASML Holding","sector":"Technology"},{"code":"INGA","name":"ING Group","sector":"Financials"}]

            Geef ALLE componenten van de index, niet een subset. Zorg dat de data zo actueel mogelijk is.
            """;

        try
        {
            var request = new
            {
                model = Model,
                max_tokens = 4000,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Claude API error {Status}: {Error}", response.StatusCode, error);
                return Array.Empty<IndexComponentResult>();
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
            var text = result?.Content?.FirstOrDefault()?.Text ?? "";

            // Parse JSON array uit de response
            var json = text.Trim();
            if (json.StartsWith("```")) json = json.Split('\n', 2).Last();
            if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
            json = json.Trim();

            var components = JsonSerializer.Deserialize<IndexComponentResult[]>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Claude retourneerde {Count} componenten voor {Index}", components?.Length ?? 0, indexName);
            return components ?? Array.Empty<IndexComponentResult>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Claude index import mislukt voor {Index}: {Message}", indexName, ex.Message);
            return Array.Empty<IndexComponentResult>();
        }
    }

    public record IndexComponentResult
    {
        [JsonPropertyName("code")] public string Code { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("sector")] public string Sector { get; init; } = "";
    }

    private record ClaudeResponse(
        [property: JsonPropertyName("content")] ClaudeContentBlock[]? Content
    );
    private record ClaudeContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text
    );
}
```

---

## Stap 3: DI registratie

Open `src/AxonStockAgent.Api/Program.cs`, voeg toe:

```csharp
builder.Services.AddScoped<ClaudeIndexService>();
```

Zorg ook dat er een HttpClient beschikbaar is (die zou al geregistreerd moeten zijn via IHttpClientFactory).

---

## Stap 4: Update `IndexImportService` — Finnhub + Claude fallback

Open `src/AxonStockAgent.Api/Services/IndexImportService.cs`.

Vervang de **volledige inhoud** door:

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
    private readonly ClaudeIndexService _claudeService;
    private readonly ILogger<IndexImportService> _logger;

    // Mapping van onze index-symbolen naar Finnhub-symbolen
    private static readonly Dictionary<string, string> FinnhubIndexMap = new()
    {
        ["GSPC.INDX"]  = "^GSPC",   // S&P 500
        ["NDX.INDX"]   = "^NDX",    // NASDAQ-100
        ["DJI.INDX"]   = "^DJI",    // Dow Jones 30
    };

    public IndexImportService(
        AppDbContext db,
        ProviderManager providers,
        ClaudeIndexService claudeService,
        ILogger<IndexImportService> logger)
    {
        _db            = db;
        _providers     = providers;
        _claudeService = claudeService;
        _logger        = logger;
    }

    /// <summary>
    /// Import via API: probeert Finnhub (voor US) of EODHD (als beschikbaar).
    /// </summary>
    public async Task<(int count, string source)> ImportViaApi(int indexId)
    {
        var index = await _db.MarketIndices.FindAsync(indexId);
        if (index == null) return (0, "not_found");

        // 1. Probeer Finnhub (gratis, voor US-indexen)
        if (FinnhubIndexMap.TryGetValue(index.IndexSymbol, out var finnhubSymbol))
        {
            var providerObj = await _providers.GetProviderByName("finnhub");
            if (providerObj is FinnhubProvider finnhub)
            {
                var tickers = await finnhub.GetIndexConstituents(finnhubSymbol);
                if (tickers.Length > 0)
                {
                    var count = await UpsertMemberships(index, tickers.Select(t => new ComponentInfo(t, "", "")).ToArray());
                    return (count, "finnhub");
                }
            }
        }

        // 2. Fallback: probeer EODHD fundamentals
        var eodhdObj = await _providers.GetProviderByName("eodhd");
        if (eodhdObj is EodhdProvider eodhd)
        {
            var components = await eodhd.GetIndexComponents(index.IndexSymbol);
            if (components.Length > 0)
            {
                var count = await UpsertMemberships(index, components.Select(c => new ComponentInfo(c.Code, c.Name, c.Sector)).ToArray());
                return (count, "eodhd");
            }
        }

        return (0, "no_data");
    }

    /// <summary>
    /// Import via Claude AI: vraag Claude om de index-samenstelling.
    /// Werkt voor alle indexen, maar vooral bedoeld voor NL (AEX, AMX, AScX).
    /// </summary>
    public async Task<(int count, string source)> ImportViaClaude(int indexId)
    {
        var index = await _db.MarketIndices.FindAsync(indexId);
        if (index == null) return (0, "not_found");

        var components = await _claudeService.GetIndexComponentsViaAI(index.DisplayName, index.ExchangeCode);
        if (components.Length == 0) return (0, "claude_empty");

        var count = await UpsertMemberships(index, components.Select(c => new ComponentInfo(c.Code, c.Name, c.Sector)).ToArray());
        return (count, "claude");
    }

    /// <summary>
    /// Bestaande methode - backward compatible wrapper.
    /// </summary>
    public async Task<int> ImportIndexComponents(int indexId)
    {
        var (count, _) = await ImportViaApi(indexId);
        return count;
    }

    // ── Shared upsert logic ──────────────────────────────────────────────

    private record ComponentInfo(string Code, string Name, string Sector);

    private async Task<int> UpsertMemberships(MarketIndexEntity index, ComponentInfo[] components)
    {
        var now = DateTime.UtcNow;
        var exchange = index.ExchangeCode;

        // Verwijder bestaande memberships
        var existing = await _db.IndexMemberships
            .Where(m => m.MarketIndexId == index.Id)
            .ToListAsync();
        _db.IndexMemberships.RemoveRange(existing);

        foreach (var c in components)
        {
            // Bouw volledig symbool: voor US-indexen via Finnhub is code al zonder suffix
            var fullSymbol = c.Code.Contains('.') ? c.Code : $"{c.Code}.{exchange}";

            _db.IndexMemberships.Add(new IndexMembershipEntity
            {
                MarketIndexId = index.Id,
                Symbol        = fullSymbol,
                Name          = c.Name,
                Sector        = c.Sector,
                AddedAt       = now,
            });

            // Zorg dat symbool ook in MarketSymbols staat
            var existsInMarket = await _db.MarketSymbols.AnyAsync(m => m.Symbol == fullSymbol);
            if (!existsInMarket)
            {
                _db.MarketSymbols.Add(new MarketSymbolEntity
                {
                    Symbol     = fullSymbol,
                    Exchange   = exchange,
                    Name       = c.Name,
                    Sector     = c.Sector,
                    Country    = index.Country,
                    IsActive   = true,
                    ImportedAt = now,
                    UpdatedAt  = now,
                });
            }
        }

        index.SymbolCount = components.Length;
        index.LastImportAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Index {Index}: {Count} componenten ge\u00efmporteerd", index.DisplayName, components.Length);
        return components.Length;
    }
}
```

---

## Stap 5: Update admin endpoints

Open `src/AxonStockAgent.Api/Controllers/AdminController.cs`.

Vervang het bestaande `ImportIndexComponents` endpoint door twee endpoints:

```csharp
/// <summary>Import via data-API (Finnhub voor US, EODHD fallback)</summary>
[HttpPost("indices/{id:int}/import")]
public async Task<IActionResult> ImportIndexViaApi(int id, [FromServices] IndexImportService importService)
{
    var index = await _db.MarketIndices.FindAsync(id);
    if (index == null) return NotFound();

    var (count, source) = await importService.ImportViaApi(id);
    return Ok(new { data = new { index = index.DisplayName, importedCount = count, source } });
}

/// <summary>Import via Claude AI (werkt voor alle indexen)</summary>
[HttpPost("indices/{id:int}/import-ai")]
public async Task<IActionResult> ImportIndexViaClaude(int id, [FromServices] IndexImportService importService)
{
    var index = await _db.MarketIndices.FindAsync(id);
    if (index == null) return NotFound();

    var (count, source) = await importService.ImportViaClaude(id);
    return Ok(new { data = new { index = index.DisplayName, importedCount = count, source } });
}
```

---

## Stap 6: Frontend — Update AdminExchangesPage

Open `frontend/src/pages/AdminExchangesPage.tsx`.

### 6a. Voeg Sparkles icon toe

Voeg `Sparkles` toe aan de lucide-react import:
```tsx
import { Plus, Trash2, Download, Globe, Sparkles } from 'lucide-react';
```

### 6b. Voeg AI import handler toe

Voeg toe naast de bestaande `handleImportIndex`:

```tsx
const [aiImportingIndex, setAiImportingIndex] = useState<number | null>(null);

async function handleAiImportIndex(id: number) {
  setAiImportingIndex(id);
  setFeedback(null);
  try {
    const result: any = await api.post(`/v1/admin/indices/${id}/import-ai`, {});
    const count = result?.data?.importedCount ?? 0;
    const source = result?.data?.source ?? 'unknown';
    if (count > 0) {
      setFeedback({ type: 'success', message: `${count} componenten ge\u00efmporteerd via ${source === 'claude' ? 'Claude AI' : source}` });
    } else {
      setFeedback({ type: 'error', message: 'Claude kon geen componenten ophalen. Controleer of de Claude API key is geconfigureerd.' });
    }
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  } catch (err: any) {
    setFeedback({ type: 'error', message: `AI import mislukt: ${err?.message ?? 'Onbekende fout'}` });
  } finally {
    setAiImportingIndex(null);
  }
}
```

### 6c. Update de handleImportIndex met beter feedback

Vervang de bestaande `handleImportIndex`:

```tsx
async function handleImportIndex(id: number) {
  setImportingIndex(id);
  setFeedback(null);
  try {
    const result: any = await api.post(`/v1/admin/indices/${id}/import`, {});
    const count = result?.data?.importedCount ?? 0;
    const source = result?.data?.source ?? 'unknown';
    if (count > 0) {
      setFeedback({ type: 'success', message: `${count} componenten ge\u00efmporteerd via ${source === 'finnhub' ? 'Finnhub' : source === 'eodhd' ? 'EODHD' : source}` });
    } else {
      setFeedback({ type: 'error', message: 'Geen data via API. Probeer "AI Import" als alternatief.' });
    }
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  } catch (err: any) {
    setFeedback({ type: 'error', message: `API import mislukt: ${err?.message ?? 'Onbekende fout'}` });
  } finally {
    setImportingIndex(null);
  }
}
```

### 6d. Voeg AI knop toe in de acties-kolom

In de index-tabel, voeg een **AI Import knop** toe naast de bestaande import-knop. De knoppen per index worden:

```tsx
<td className="px-4 py-3">
  <div className="flex items-center justify-center gap-2">
    {/* API Import (Finnhub/EODHD) */}
    <button
      onClick={() => handleImportIndex(idx.id)}
      disabled={importingIndex === idx.id}
      className="p-1.5 rounded bg-blue-500/15 text-blue-400 hover:bg-blue-500/25 transition-colors disabled:opacity-50"
      title="Import via API (Finnhub/EODHD)"
    >
      <Download size={14} className={importingIndex === idx.id ? 'animate-bounce' : ''} />
    </button>

    {/* AI Import (Claude) */}
    <button
      onClick={() => handleAiImportIndex(idx.id)}
      disabled={aiImportingIndex === idx.id}
      className="p-1.5 rounded bg-purple-500/15 text-purple-400 hover:bg-purple-500/25 transition-colors disabled:opacity-50"
      title="Import via Claude AI"
    >
      <Sparkles size={14} className={aiImportingIndex === idx.id ? 'animate-pulse' : ''} />
    </button>

    {/* Delete */}
    <button
      onClick={() => { if (window.confirm(`Index ${idx.displayName} verwijderen?`)) deleteIndexMutation.mutate(idx.id); }}
      className="p-1.5 rounded text-gray-600 hover:text-red-400 transition-colors"
      title="Verwijder index"
    >
      <Trash2 size={14} />
    </button>
  </div>
</td>
```

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `src/.../Providers/FinnhubProvider.cs` | **Gewijzigd** — `GetIndexConstituents` methode |
| `src/.../Services/ClaudeIndexService.cs` | **Nieuw** — Claude API voor index-componenten |
| `src/.../Services/IndexImportService.cs` | **Herschreven** — Finnhub + Claude + EODHD fallback chain |
| `src/.../Controllers/AdminController.cs` | **Gewijzigd** — `import` + `import-ai` endpoints |
| `src/.../Program.cs` | **Gewijzigd** — DI registratie |
| `frontend/src/pages/AdminExchangesPage.tsx` | **Gewijzigd** — AI import knop + feedback |

## Na de prompt

Per index zijn er nu drie knoppen:
1. **⬇ Blauw (Download)** — API import: Finnhub voor US, EODHD fallback
2. **✨ Paars (Sparkles)** — AI import: Claude genereert de samenstelling
3. **🗑 Rood (Trash)** — Verwijder

Workflow:
- US-indexen (S&P 500, NDX, DJI): klik blauw → Finnhub levert de data gratis
- NL-indexen (AEX, AMX, AScX): klik paars → Claude levert de componenten
- Na import: symbolen verschijnen op het Markets-scherm in de juiste kolom
