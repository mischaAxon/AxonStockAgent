using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class WatchlistController : ControllerBase
{
    private readonly AppDbContext _db;

    public WatchlistController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Watchlist
            .Where(w => w.IsActive)
            .OrderBy(w => w.Symbol)
            .ToListAsync();
        return Ok(new { data = items });
    }

    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(string symbol)
    {
        var item = await _db.Watchlist.FirstOrDefaultAsync(w => w.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();
        return Ok(new { data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddWatchlistRequest request)
    {
        var existing = await _db.Watchlist.FirstOrDefaultAsync(w => w.Symbol == request.Symbol.ToUpper());
        if (existing != null)
        {
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Watchlist.Add(new WatchlistItem
            {
                Symbol = request.Symbol.ToUpper(),
                Exchange = request.Exchange,
                Name = request.Name,
                Sector = request.Sector,
                IsActive = true,
                AddedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        return Created($"/api/v1/watchlist/{request.Symbol}", new { data = request });
    }

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol)
    {
        var item = await _db.Watchlist.FirstOrDefaultAsync(w => w.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();
        item.IsActive = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record AddWatchlistRequest(string Symbol, string? Exchange = null, string? Name = null, string? Sector = null);
