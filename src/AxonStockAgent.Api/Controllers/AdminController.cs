using System.Net.Http.Json;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;
using AxonStockAgent.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly AlgoSettingsService _algoSettings;

    public AdminController(AppDbContext db, ProviderManager providers, AlgoSettingsService algoSettings)
    {
        _db           = db;
        _providers    = providers;
        _algoSettings = algoSettings;
    }

    // ── Users ──────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id, u.Email, u.DisplayName, u.Role,
                u.IsActive, u.CreatedAt, u.LastLoginAt
            })
            .ToListAsync();
        return Ok(new { data = users });
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (request.Role     != null)  user.Role     = request.Role;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync();
        return Ok(new { data = new { user.Id, user.Email, user.Role, user.IsActive } });
    }

    // ── Settings (Algo Config) ─────────────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var grouped = await _algoSettings.GetAllGroupedAsync();
        return Ok(new { data = grouped });
    }

    [HttpPut("settings/{id:int}")]
    public async Task<IActionResult> UpdateSetting(int id, [FromBody] UpdateSettingRequest request)
    {
        try
        {
            var updated = await _algoSettings.UpdateSettingAsync(id, request.Value);
            return Ok(new { data = updated });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("settings/reset")]
    public async Task<IActionResult> ResetSettings()
    {
        await _algoSettings.ResetToDefaultsAsync();
        var grouped = await _algoSettings.GetAllGroupedAsync();
        return Ok(new { data = grouped, message = "Settings gereset naar standaardwaarden" });
    }

    // ── Providers ──────────────────────────────────────────────────────────────

    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders()
    {
        var providers = await _db.DataProviders
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.DisplayName, p.ProviderType,
                p.IsEnabled, p.Priority, p.RateLimitPerMinute,
                p.SupportsEu, p.SupportsUs, p.IsFree, p.MonthlyCost,
                p.HealthStatus, p.LastHealthCheck, p.UpdatedAt,
                hasApiKey = p.ApiKeyEncrypted != null
            })
            .ToListAsync();

        return Ok(new { data = providers });
    }

    [HttpPut("providers/{name}")]
    public async Task<IActionResult> UpdateProvider(string name, [FromBody] UpdateProviderRequest request)
    {
        var provider = await _db.DataProviders.FirstOrDefaultAsync(p => p.Name == name);
        if (provider == null) return NotFound(new { error = $"Provider '{name}' niet gevonden" });

        if (request.IsEnabled.HasValue) provider.IsEnabled = request.IsEnabled.Value;
        if (request.Priority.HasValue)  provider.Priority  = Math.Max(1, request.Priority.Value);
        if (request.ApiKey    != null)  provider.ApiKeyEncrypted = request.ApiKey; // TODO: encrypt
        if (request.ConfigJson != null) provider.ConfigJson = request.ConfigJson;
        provider.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { data = new { provider.Name, provider.IsEnabled, provider.HealthStatus } });
    }

    [HttpPost("providers/{name}/test")]
    public async Task<IActionResult> TestProvider(string name)
    {
        var config = await _db.DataProviders.FirstOrDefaultAsync(p => p.Name == name);
        if (config == null) return NotFound(new { error = $"Provider '{name}' niet gevonden" });
        if (!config.IsEnabled) return BadRequest(new { error = "Provider is niet ingeschakeld" });

        // Special case: Claude AI provider (geen standaard interface)
        if (name == "claude")
        {
            var keyProvider = HttpContext.RequestServices.GetRequiredService<ClaudeApiKeyProvider>();
            var apiKey = await keyProvider.GetApiKeyAsync();

            if (string.IsNullOrEmpty(apiKey))
            {
                config.HealthStatus    = "down";
                config.LastHealthCheck = DateTime.UtcNow;
                config.UpdatedAt       = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return Ok(new { data = new { name, health = "down", detail = "Geen API key geconfigureerd", checkedAt = config.LastHealthCheck } });
            }

            string claudeHealth;
            string? claudeDetail = null;
            try
            {
                var httpFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var http = httpFactory.CreateClient();
                var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = JsonContent.Create(new
                    {
                        model      = "claude-sonnet-4-20250514",
                        max_tokens = 10,
                        messages   = new[] { new { role = "user", content = "ping" } }
                    })
                };
                req.Headers.Add("x-api-key", apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
                var response = await http.SendAsync(req);
                claudeHealth = response.IsSuccessStatusCode ? "healthy" : "degraded";
                claudeDetail = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                claudeHealth = "down";
                claudeDetail = ex.Message;
            }

            config.HealthStatus    = claudeHealth;
            config.LastHealthCheck = DateTime.UtcNow;
            config.UpdatedAt       = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { data = new { name, health = claudeHealth, detail = claudeDetail, checkedAt = config.LastHealthCheck } });
        }

        var provider = await _providers.GetProviderByName(name);
        if (provider == null)
        {
            config.HealthStatus    = "unknown";
            config.LastHealthCheck = DateTime.UtcNow;
            config.UpdatedAt       = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { data = new { name, health = "unknown", detail = "Provider heeft nog geen implementatie in deze versie", checkedAt = config.LastHealthCheck } });
        }

        string health;
        string? detail = null;

        try
        {
            if (provider is IMarketDataProvider md)
            {
                var candles = await md.GetCandles("AAPL", "D", 2);
                health = candles != null && candles.Length > 0 ? "healthy" : "degraded";
            }
            else if (provider is INewsProvider np)
            {
                var news = await np.GetNews(limit: 1);
                health = news.Length > 0 ? "healthy" : "degraded";
            }
            else if (provider is IFundamentalsProvider fp)
            {
                var profile = await fp.GetProfile("AAPL");
                health = profile != null ? "healthy" : "degraded";
            }
            else
            {
                health = "unknown";
            }
        }
        catch (Exception ex)
        {
            health = "down";
            detail = ex.Message;
        }

        // Sla health status op
        config.HealthStatus    = health;
        config.LastHealthCheck = DateTime.UtcNow;
        config.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { data = new { name, health, detail, checkedAt = config.LastHealthCheck } });
    }

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

    /// <summary>
    /// Vult een index met alle actieve MarketSymbols van de gekoppelde exchange.
    /// Fallback wanneer EODHD fundamentals niet beschikbaar is.
    /// </summary>
    [HttpPost("indices/{id:int}/fill-from-exchange")]
    public async Task<IActionResult> FillIndexFromExchange(int id)
    {
        var index = await _db.MarketIndices.FindAsync(id);
        if (index == null) return NotFound();

        if (string.IsNullOrEmpty(index.ExchangeCode))
            return BadRequest(new { error = "Index heeft geen exchange code geconfigureerd." });

        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive && m.Exchange == index.ExchangeCode)
            .ToListAsync();

        if (symbols.Count == 0)
            return BadRequest(new { error = $"Geen symbolen gevonden voor exchange '{index.ExchangeCode}'. Importeer eerst de beurs." });

        var now = DateTime.UtcNow;

        // Verwijder bestaande memberships
        var existing = await _db.IndexMemberships.Where(m => m.MarketIndexId == id).ToListAsync();
        _db.IndexMemberships.RemoveRange(existing);

        // Voeg alle exchange-symbolen toe als leden
        foreach (var sym in symbols)
        {
            _db.IndexMemberships.Add(new IndexMembershipEntity
            {
                MarketIndexId = id,
                Symbol        = sym.Symbol,
                Name          = sym.Name,
                Sector        = sym.Sector,
                Industry      = sym.Industry,
                AddedAt       = now,
            });
        }

        index.SymbolCount = symbols.Count;
        index.LastImportAt = now;
        await _db.SaveChangesAsync();

        return Ok(new { data = new { index = index.DisplayName, importedCount = symbols.Count } });
    }

    /// <summary>
    /// Herlaad AEX, AMX en AMS Next 20 met correcte hardcoded data (Euronext, maart 2026).
    /// Vervangt de onnauwkeurige Claude AI-geïmporteerde data.
    /// </summary>
    [HttpPost("indices/reload-nl")]
    public async Task<IActionResult> ReloadDutchIndices()
    {
        var results = new List<object>();
        var now = DateTime.UtcNow;

        var indexMappings = new[]
        {
            new { Symbol = "AEX.INDX",   DisplayName = "AEX",           Components = DutchIndexData.AEX },
            new { Symbol = "AMX.INDX",   DisplayName = "AMX Midcap",    Components = DutchIndexData.AMX },
            new { Symbol = "ASCX.INDX",  DisplayName = "AMS Next 20",   Components = DutchIndexData.AmsNext20 },
        };

        foreach (var mapping in indexMappings)
        {
            // Zoek de index op symbol of op eerste woord van displaynaam (case-insensitive)
            var firstWord = mapping.DisplayName.Split(' ')[0].ToLower();
            var index = await _db.MarketIndices.FirstOrDefaultAsync(i =>
                i.IndexSymbol == mapping.Symbol ||
                i.DisplayName.ToLower().Contains(firstWord));

            if (index == null)
            {
                index = new MarketIndexEntity
                {
                    IndexSymbol  = mapping.Symbol,
                    DisplayName  = mapping.DisplayName,
                    ExchangeCode = "AS",
                    Country      = "NL",
                    IsEnabled    = true,
                };
                _db.MarketIndices.Add(index);
                await _db.SaveChangesAsync(); // nodig voor Id
            }

            // Verwijder bestaande memberships
            var existing = await _db.IndexMemberships
                .Where(m => m.MarketIndexId == index.Id)
                .ToListAsync();
            _db.IndexMemberships.RemoveRange(existing);

            // Voeg correcte componenten toe
            foreach (var comp in mapping.Components)
            {
                var fullSymbol = $"{comp.Ticker}.AS";

                _db.IndexMemberships.Add(new IndexMembershipEntity
                {
                    MarketIndexId = index.Id,
                    Symbol        = fullSymbol,
                    Name          = comp.Name,
                    Sector        = comp.Sector,
                    AddedAt       = now,
                });

                // Zorg dat het symbool ook in MarketSymbols staat
                var alreadyExists = await _db.MarketSymbols.AnyAsync(m => m.Symbol == fullSymbol);
                if (!alreadyExists)
                {
                    _db.MarketSymbols.Add(new MarketSymbolEntity
                    {
                        Symbol     = fullSymbol,
                        Exchange   = "AS",
                        Name       = comp.Name,
                        Sector     = comp.Sector,
                        Country    = "NL",
                        IsActive   = true,
                        ImportedAt = now,
                        UpdatedAt  = now,
                    });
                }
            }

            index.SymbolCount  = mapping.Components.Length;
            index.LastImportAt = now;
            index.DisplayName  = mapping.DisplayName;

            results.Add(new { index = mapping.DisplayName, count = mapping.Components.Length });
        }

        await _db.SaveChangesAsync();
        return Ok(new { data = results, message = "NL-indexen succesvol herladen met correcte data" });
    }

    // ── Scan trigger ───────────────────────────────────────────────────────────

    /// <summary>Zet een handmatige scan trigger klaar. De worker pikt deze op binnen ~1 minuut.</summary>
    [HttpPost("scan/trigger")]
    public async Task<IActionResult> TriggerScan([FromBody] TriggerScanRequest? request = null)
    {
        // Annuleer eventueel nog openstaande pending triggers
        var pending = await _db.ScanTriggers
            .Where(t => t.Status == "pending")
            .ToListAsync();
        foreach (var p in pending)
            p.Status = "cancelled";

        var trigger = new ScanTriggerEntity
        {
            Status      = "pending",
            RequestedBy = request?.RequestedBy ?? "admin",
            CreatedAt   = DateTime.UtcNow,
        };
        _db.ScanTriggers.Add(trigger);
        await _db.SaveChangesAsync();

        return Ok(new { data = new { triggerId = trigger.Id, status = trigger.Status, message = "Scan trigger aangemaakt, worker pikt dit op binnen ~1 minuut" } });
    }

    /// <summary>Haal de status op van de laatste scan trigger.</summary>
    [HttpGet("scan/status")]
    public async Task<IActionResult> GetScanStatus()
    {
        var latest = await _db.ScanTriggers
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (latest == null)
            return Ok(new { data = (object?)null, message = "Nog geen scan triggers aangemaakt" });

        return Ok(new { data = new
        {
            latest.Id,
            latest.Status,
            latest.RequestedBy,
            latest.CreatedAt,
            latest.StartedAt,
            latest.CompletedAt,
            latest.ProcessedCount,
            latest.SignalsCount,
            latest.ErrorMessage,
        }});
    }

    // ── Fundamentals Bulk Refresh ──────────────────────────────────────────────

    /// <summary>
    /// Refresh fundamentals voor alle actieve MarketSymbols.
    /// Duurt ~3-4 minuten voor 98 symbolen (EODHD rate limited).
    /// </summary>
    [HttpPost("fundamentals/refresh-all")]
    public async Task<IActionResult> RefreshAllFundamentals([FromServices] FundamentalsService fundamentalsService)
    {
        var (total, success, failed) = await fundamentalsService.RefreshAllMarketSymbolsFundamentals();

        return Ok(new
        {
            data = new { total, success, failed },
            message = $"Fundamentals refresh voltooid: {success}/{total} succesvol"
        });
    }

    /// <summary>Toont de huidige staat van fundamentals data.</summary>
    [HttpGet("fundamentals/status")]
    public async Task<IActionResult> GetFundamentalsStatus()
    {
        var totalSymbols = await _db.MarketSymbols.CountAsync(m => m.IsActive);
        var withFundamentals = await _db.CompanyFundamentals.CountAsync();
        var recentFundamentals = await _db.CompanyFundamentals
            .CountAsync(f => f.FetchedAt >= DateTime.UtcNow.AddHours(-24));
        var staleFundamentals = await _db.CompanyFundamentals
            .CountAsync(f => f.FetchedAt < DateTime.UtcNow.AddDays(-7));

        var oldestFetch = await _db.CompanyFundamentals
            .OrderBy(f => f.FetchedAt)
            .Select(f => f.FetchedAt)
            .FirstOrDefaultAsync();

        var newestFetch = await _db.CompanyFundamentals
            .OrderByDescending(f => f.FetchedAt)
            .Select(f => f.FetchedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            data = new
            {
                totalSymbols,
                withFundamentals,
                missingFundamentals = totalSymbols - withFundamentals,
                recentLast24h = recentFundamentals,
                staleOlderThan7d = staleFundamentals,
                oldestFetch,
                newestFetch
            }
        });
    }

    // ── Symbol Cleanup ─────────────────────────────────────────────────────────

    /// <summary>
    /// Toont alle "orphan" symbolen: actieve MarketSymbols die NIET in een index zitten.
    /// </summary>
    [HttpGet("symbols/orphans")]
    public async Task<IActionResult> GetOrphanSymbols()
    {
        var indexedSymbols = await _db.IndexMemberships
            .Select(m => m.Symbol)
            .Distinct()
            .ToListAsync();

        var indexedSet = new HashSet<string>(indexedSymbols);

        var allActive = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .OrderBy(m => m.Symbol)
            .ToListAsync();

        var orphanList = allActive
            .Where(m => !indexedSet.Contains(m.Symbol))
            .Select(m => new { m.Symbol, m.Name, m.Exchange, m.Country, m.Sector, m.ImportedAt })
            .ToList();

        return Ok(new
        {
            data = new
            {
                totalActive   = allActive.Count,
                totalIndexed  = indexedSymbols.Count,
                orphanCount   = orphanList.Count,
                orphans       = orphanList
            }
        });
    }

    /// <summary>
    /// Deactiveer alle orphan symbolen (symbolen die niet in een index zitten).
    /// Ze worden niet verwijderd maar op IsActive = false gezet.
    /// Gebruik ?keep=SYM1.AS,SYM2.AS om specifieke symbolen te bewaren.
    /// </summary>
    [HttpPost("symbols/cleanup-orphans")]
    public async Task<IActionResult> CleanupOrphanSymbols([FromQuery] string? keep = null)
    {
        var indexedSymbols = await _db.IndexMemberships
            .Select(m => m.Symbol)
            .Distinct()
            .ToListAsync();

        var indexedSet = new HashSet<string>(indexedSymbols);

        var keepSet = new HashSet<string>(
            (keep ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpper()),
            StringComparer.OrdinalIgnoreCase);

        var allActive = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .ToListAsync();

        var toDeactivate = allActive
            .Where(m => !indexedSet.Contains(m.Symbol) && !keepSet.Contains(m.Symbol))
            .ToList();

        foreach (var sym in toDeactivate)
        {
            sym.IsActive  = false;
            sym.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            data = new
            {
                deactivated = toDeactivate.Count,
                kept        = keepSet.Count,
                symbols     = toDeactivate.Select(s => s.Symbol).ToList()
            },
            message = $"{toDeactivate.Count} orphan symbolen gedeactiveerd"
        });
    }

    // ── News Fetch ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Trigger handmatig het ophalen van nieuws voor alle actieve symbolen.
    /// Duurt ~35 seconden voor 69 symbolen (500ms rate limit per symbool).
    /// </summary>
    [HttpPost("news/fetch")]
    public async Task<IActionResult> FetchNews([FromServices] NewsService newsService)
    {
        var before = await _db.NewsArticles.CountAsync();

        await newsService.FetchLatestNews();
        await newsService.CalculateSectorSentiment();

        var after = await _db.NewsArticles.CountAsync();
        var newArticles = after - before;

        return Ok(new
        {
            data = new { totalArticles = after, newArticles },
            message = $"{newArticles} nieuwe artikelen opgehaald"
        });
    }

    /// <summary>Update sector-veld voor oude artikelen die een symbool maar geen sector hebben.</summary>
    [HttpPost("news/backfill-sectors")]
    public async Task<IActionResult> BackfillNewsSectors([FromServices] NewsService newsService)
    {
        var updated = await newsService.BackfillSectors();
        return Ok(new { data = new { updated }, message = $"{updated} artikelen bijgewerkt met sector" });
    }

    /// <summary>
    /// Toont de status van de nieuws database.
    /// </summary>
    [HttpGet("news/status")]
    public async Task<IActionResult> GetNewsStatus()
    {
        var total = await _db.NewsArticles.CountAsync();
        var last24h = await _db.NewsArticles.CountAsync(n => n.PublishedAt >= DateTime.UtcNow.AddHours(-24));
        var last7d  = await _db.NewsArticles.CountAsync(n => n.PublishedAt >= DateTime.UtcNow.AddDays(-7));

        var symbolsWithNews = await _db.NewsArticles
            .Where(n => n.Symbol != null && n.PublishedAt >= DateTime.UtcNow.AddDays(-7))
            .Select(n => n.Symbol)
            .Distinct()
            .CountAsync();

        var sectors = await _db.NewsArticles
            .Where(n => n.Sector != null && n.PublishedAt >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(n => n.Sector!)
            .Select(g => new { sector = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync();

        var oldestArticle = await _db.NewsArticles
            .OrderBy(n => n.PublishedAt)
            .Select(n => (DateTime?)n.PublishedAt)
            .FirstOrDefaultAsync();

        var newestArticle = await _db.NewsArticles
            .OrderByDescending(n => n.PublishedAt)
            .Select(n => (DateTime?)n.PublishedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            data = new
            {
                total,
                last24h,
                last7d,
                symbolsWithNews,
                sectorBreakdown = sectors,
                oldestArticle,
                newestArticle
            }
        });
    }
}

public record UpdateUserRequest(string? Role = null, bool? IsActive = null);
public record TriggerScanRequest(string? RequestedBy = null);
public record UpdateProviderRequest(bool? IsEnabled = null, int? Priority = null, string? ApiKey = null, string? ConfigJson = null);
public record UpdateSettingRequest(string Value);
public record AddExchangeRequest(string ExchangeCode, string? DisplayName = null, string? Country = null);
public record UpdateExchangeRequest(bool? IsEnabled = null, string? DisplayName = null);
public record AddIndexRequest(string IndexSymbol, string? DisplayName = null, string? ExchangeCode = null, string? Country = null);
