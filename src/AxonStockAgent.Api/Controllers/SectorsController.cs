using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/sectors")]
public class SectorsController : ControllerBase
{
    private readonly SectorService _sectors;
    private readonly AppDbContext  _db;

    public SectorsController(SectorService sectors, AppDbContext db)
    {
        _sectors = sectors;
        _db      = db;
    }

    /// <summary>Lijst van alle unieke sectoren met het aantal aandelen.</summary>
    [HttpGet]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _sectors.GetSectorSummary();
        return Ok(new { data = summary });
    }

    /// <summary>Alle watchlist-symbolen in een specifieke sector.</summary>
    [HttpGet("{sector}/symbols")]
    public async Task<IActionResult> GetSymbolsBySector(string sector)
    {
        var items = await _db.Watchlist
            .Where(w => w.IsActive && w.Sector == sector)
            .OrderBy(w => w.Symbol)
            .Select(w => new
            {
                w.Symbol, w.Name, w.Industry,
                w.Country, w.MarketCap, w.Logo
            })
            .ToListAsync();
        return Ok(new { data = items });
    }

    /// <summary>Verrijkt alle watchlist-items zonder sector info (admin only).</summary>
    [HttpPost("enrich")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> EnrichAll()
    {
        var count = await _sectors.EnrichAllWatchlist();
        return Ok(new { data = new { enriched = count } });
    }

    /// <summary>Handmatige sector-override voor een symbool (admin only).</summary>
    [HttpPut("{symbol}/sector")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetSector(string symbol, [FromBody] SetSectorRequest request)
    {
        var item = await _db.Watchlist
            .FirstOrDefaultAsync(w => w.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();

        item.Sector       = request.Sector;
        item.Industry     = request.Industry;
        item.SectorSource = "manual";
        item.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { data = new { item.Symbol, item.Sector, item.Industry, item.SectorSource } });
    }
}

public record SetSectorRequest(string? Sector, string? Industry);
