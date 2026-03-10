using AxonStockAgent.Api.Data;
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

    public AdminController(AppDbContext db, ProviderManager providers)
    {
        _db        = db;
        _providers = providers;
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

    // ── Settings ───────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    public IActionResult GetSettings() => Ok(new { data = new { } });

    [HttpPut("settings")]
    public IActionResult UpdateSettings() => Ok();

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
}

public record UpdateUserRequest(string? Role = null, bool? IsActive = null);
public record UpdateProviderRequest(bool? IsEnabled = null, string? ApiKey = null, string? ConfigJson = null);
