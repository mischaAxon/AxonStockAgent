# Prompt 22 — Claude API als Provider: API key beheren via admin

## Doel

De Claude API key wordt nu op 3 plekken anders gelezen (env vars, IConfiguration, ScreenerConfig). Centraliseer dit: Claude wordt een provider in de `data_providers` tabel, net als Finnhub en EODHD. De API key wordt via de admin Providers-pagina beheerd.

## Verificatie

```bash
cd src/AxonStockAgent.Api && dotnet build --nologo -v quiet
cd src/AxonStockAgent.Worker && dotnet build --nologo -v quiet
cd frontend && npx tsc --noEmit && npm run build
```

---

## Stap 1: Seed Claude als provider in de database

Voeg een Claude provider record toe aan de `data_providers` tabel. Dit kan via een SQL insert of via de seed-data in `AppDbContext`.

Zoek in `src/AxonStockAgent.Api/Data/AppDbContext.cs` of er seed data is voor providers. Als er een `HasData()` call is voor `DataProviderEntity`, voeg Claude toe:

```csharp
new DataProviderEntity
{
    Id = 3, // of het volgende vrije ID
    Name = "claude",
    DisplayName = "Claude AI (Anthropic)",
    ProviderType = "ai",
    IsEnabled = false,
    RateLimitPerMinute = 50,
    SupportsEu = true,
    SupportsUs = true,
    IsFree = false,
    MonthlyCost = 0, // pay per use
    HealthStatus = "unknown"
}
```

Als er geen seed data is, voeg dan een SQL migratie toe of een runtime-check die het record aanmaakt als het niet bestaat. De eenvoudigste aanpak is een `EnsureClaudeProvider` methode die bij startup draait:

Voeg toe in `Program.cs` van het API project (na de database migratie/startup sectie):

```csharp
// Ensure Claude provider exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.DataProviders.AnyAsync(p => p.Name == "claude"))
    {
        db.DataProviders.Add(new DataProviderEntity
        {
            Name = "claude",
            DisplayName = "Claude AI (Anthropic)",
            ProviderType = "ai",
            IsEnabled = false,
            RateLimitPerMinute = 50,
            SupportsEu = true,
            SupportsUs = true,
            IsFree = false,
            MonthlyCost = 0,
            HealthStatus = "unknown",
        });
        await db.SaveChangesAsync();
    }
}
```

Doe hetzelfde in `Program.cs` van het Worker project.

Vergeet niet de benodigde usings toe te voegen (`AxonStockAgent.Api.Data`, `AxonStockAgent.Api.Data.Entities`, `Microsoft.EntityFrameworkCore`).

---

## Stap 2: Maak een `ClaudeApiKeyProvider` helper service

Deze service leest de Claude API key uit de `data_providers` tabel, met fallback naar environment variabelen (voor backward compatibility).

Maak `src/AxonStockAgent.Api/Services/ClaudeApiKeyProvider.cs`:

```csharp
using AxonStockAgent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Leest de Claude API key uit de data_providers tabel.
/// Fallback naar IConfiguration voor backward compatibility.
/// </summary>
public class ClaudeApiKeyProvider
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ClaudeApiKeyProvider(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Haalt de Claude API key op. Prioriteit:
    /// 1. data_providers tabel (naam = "claude")
    /// 2. IConfiguration["Claude:ApiKey"]
    /// 3. IConfiguration["ANTHROPIC_API_KEY"]
    /// 4. Environment variable ANTHROPIC_API_KEY
    /// </summary>
    public async Task<string?> GetApiKeyAsync()
    {
        // 1. Database provider
        var provider = await _db.DataProviders
            .FirstOrDefaultAsync(p => p.Name == "claude" && p.IsEnabled);
        if (provider != null && !string.IsNullOrEmpty(provider.ApiKeyEncrypted))
            return provider.ApiKeyEncrypted; // TODO: decrypt wanneer encryptie is geïmplementeerd

        // 2-4. Fallback naar config/env
        return _config["Claude:ApiKey"]
            ?? _config["ANTHROPIC_API_KEY"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    /// <summary>
    /// Check of Claude beschikbaar en geconfigureerd is.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        var key = await GetApiKeyAsync();
        return !string.IsNullOrEmpty(key);
    }
}
```

---

## Stap 3: Registreer in DI

Open `src/AxonStockAgent.Api/Program.cs`, voeg toe:
```csharp
builder.Services.AddScoped<ClaudeApiKeyProvider>();
```

Open `src/AxonStockAgent.Worker/Program.cs`, voeg toe:
```csharp
builder.Services.AddScoped<ClaudeApiKeyProvider>();
```

(De Worker heeft al een referentie naar het Api project voor AppDbContext etc.)

---

## Stap 4: Update `ClaudeIndexService` — gebruik `ClaudeApiKeyProvider`

Open `src/AxonStockAgent.Api/Services/ClaudeIndexService.cs`.

Wijzig de constructor:

**Was:**
```csharp
public ClaudeIndexService(HttpClient http, IConfiguration config, ILogger<ClaudeIndexService> logger)
{
    _http = http;
    _config = config;
    _logger = logger;
}
```

**Wordt:**
```csharp
private readonly ClaudeApiKeyProvider _keyProvider;

public ClaudeIndexService(HttpClient http, ClaudeApiKeyProvider keyProvider, ILogger<ClaudeIndexService> logger)
{
    _http = http;
    _keyProvider = keyProvider;
    _logger = logger;
}
```

Verwijder het `_config` veld.

Wijzig in `GetIndexComponentsViaAI` het ophalen van de API key:

**Was:**
```csharp
var apiKey = _config["Claude:ApiKey"] ?? _config["ANTHROPIC_API_KEY"] ?? "";
```

**Wordt:**
```csharp
var apiKey = await _keyProvider.GetApiKeyAsync() ?? "";
```

---

## Stap 5: Update `ScreenerWorker` — gebruik `ClaudeApiKeyProvider`

Open `src/AxonStockAgent.Worker/ScreenerWorker.cs`.

In de `RunScanCycleAsync` methode, zoek waar `ClaudeAnalysisService` wordt geïnstantieerd:

**Was:**
```csharp
var claudeService = new ClaudeAnalysisService(
    httpFactory.CreateClient("claude"),
    _config.ClaudeApiKey,
    loggerFactory.CreateLogger<ClaudeAnalysisService>(),
    _scopeFactory);
```

**Wordt:**
```csharp
var claudeKeyProvider = scope.ServiceProvider.GetRequiredService<ClaudeApiKeyProvider>();
var claudeApiKey = await claudeKeyProvider.GetApiKeyAsync() ?? "";

var claudeService = new ClaudeAnalysisService(
    httpFactory.CreateClient("claude"),
    claudeApiKey,
    loggerFactory.CreateLogger<ClaudeAnalysisService>(),
    _scopeFactory);
```

Dit haalt de key nu uit de database (provider tabel) met fallback naar env vars.

De `_config.ClaudeApiKey` referentie wordt niet meer gebruikt voor Claude. Het `ScreenerConfig.ClaudeApiKey` veld mag blijven bestaan als backward-compatible fallback, maar wordt niet meer de primaire bron.

---

## Stap 6: Update `ClaudeAnalysisService` — check of key leeg is

Open `src/AxonStockAgent.Worker/Services/ClaudeAnalysisService.cs`.

Deze service krijgt de key al als constructor parameter, dus hier hoeft niets te veranderen. De lege-key check (`if (string.IsNullOrEmpty(_apiKey))`) werkt al correct.

---

## Stap 7: Admin test endpoint voor Claude

De bestaande `TestProvider` endpoint in `AdminController.cs` kent alleen `IMarketDataProvider`, `INewsProvider` en `IFundamentalsProvider`. Claude is geen van deze. Voeg een special case toe.

Open `src/AxonStockAgent.Api/Controllers/AdminController.cs`.

In de `TestProvider` methode, voeg een case toe **vóór** de `if (provider == null)` check:

```csharp
// Special case: Claude AI provider (geen standaard interface)
if (name == "claude")
{
    var keyProvider = HttpContext.RequestServices.GetRequiredService<ClaudeApiKeyProvider>();
    var apiKey = await keyProvider.GetApiKeyAsync();

    if (string.IsNullOrEmpty(apiKey))
    {
        config.HealthStatus = "down";
        config.LastHealthCheck = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { data = new { name, health = "down", detail = "Geen API key geconfigureerd", checkedAt = config.LastHealthCheck } });
    }

    // Doe een minimale API call om te checken of de key werkt
    try
    {
        var httpFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
        var http = httpFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(new { model = "claude-sonnet-4-20250514", max_tokens = 10, messages = new[] { new { role = "user", content = "ping" } } })
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        var response = await http.SendAsync(request);

        health = response.IsSuccessStatusCode ? "healthy" : "degraded";
        detail = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
    }
    catch (Exception ex)
    {
        health = "down";
        detail = ex.Message;
    }

    config.HealthStatus = health;
    config.LastHealthCheck = DateTime.UtcNow;
    config.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    return Ok(new { data = new { name, health, detail, checkedAt = config.LastHealthCheck } });
}
```

Dit vereist een `using System.Net.Http.Json;` import bovenaan als die er nog niet is. En declareer `health` en `detail` vóór het blok (of pas de structuur aan zodat het compileert — de bestaande code gebruikt deze variabelen al).

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `src/.../Services/ClaudeApiKeyProvider.cs` | **Nieuw** — centrale key provider |
| `src/.../Services/ClaudeIndexService.cs` | **Gewijzigd** — gebruikt ClaudeApiKeyProvider |
| `src/.../Controllers/AdminController.cs` | **Gewijzigd** — Claude test endpoint |
| `src/.../Program.cs` (API) | **Gewijzigd** — DI + ensure Claude provider record |
| `src/AxonStockAgent.Worker/Program.cs` | **Gewijzigd** — DI + ensure Claude provider record |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — leest key via ClaudeApiKeyProvider |

## Na de prompt

1. Ga naar **Admin → Providers**
2. Je ziet nu "Claude AI (Anthropic)" als provider
3. Klik **Edit** → voer je Anthropic API key in → schakel in
4. Klik **Test** → stuurt een minimale ping naar Claude API
5. Alle services (index import, screener worker) gebruiken nu deze key
6. De oude env vars (`ANTHROPIC_API_KEY`, `Claude:ApiKey`) werken nog als fallback
