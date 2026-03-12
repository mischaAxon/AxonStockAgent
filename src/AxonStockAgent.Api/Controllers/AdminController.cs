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
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.DisplayName, p.ProviderType,
                p.IsEnabled, p.RateLimitPerMinute,
                p.SupportsEu, p.SupportsUs, p.IsFree, p.MonthlyCost,
                p.HealthStatus, p.LastHealthCheck, p.UpdatedAt,
                hasApiKey = p.ApiKeyEncrypted != null
                // ApiKeyEncrypted wordt nooit teruggestuurd
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

        var provider = await _providers.GetProviderByName(name);
        if (provider == null)
            return StatusCode(501, new { error = "Geen implementatie beschikbaar voor deze provider" });

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
}

public record UpdateUserRequest(string? Role = null, bool? IsActive = null);
public record UpdateProviderRequest(bool? IsEnabled = null, string? ApiKey = null, string? ConfigJson = null);
public record UpdateSettingRequest(string Value);
public record AddExchangeRequest(string ExchangeCode, string? DisplayName = null, string? Country = null);
public record UpdateExchangeRequest(bool? IsEnabled = null, string? DisplayName = null);
public record AddIndexRequest(string IndexSymbol, string? DisplayName = null, string? ExchangeCode = null, string? Country = null);
