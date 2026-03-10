using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class WatchlistController : ControllerBase
{
    private readonly AppDbContext         _db;
    private readonly IServiceScopeFactory _scopeFactory;

    public WatchlistController(AppDbContext db, IServiceScopeFactory scopeFactory)
    {
        _db           = db;
        _scopeFactory = scopeFactory;
    }

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
        var item = await _db.Watchlist
            .FirstOrDefaultAsync(w => w.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();
        return Ok(new { data = item });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddWatchlistRequest request)
    {
        var symbol   = request.Symbol.ToUpper();
        var existing = await _db.Watchlist.FirstOrDefaultAsync(w => w.Symbol == symbol);

        if (existing != null)
        {
            existing.IsActive  = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Watchlist.Add(new WatchlistItem
            {
                Symbol    = symbol,
                Exchange  = request.Exchange,
                Name      = request.Name,
                Sector    = request.Sector,
                IsActive  = true,
                AddedAt   = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        // Fire-and-forget: verrijk het symbool met sector info via de fundamentals provider.
        // Gebruikt een nieuwe scope zodat de scoped services veilig kunnen worden gebruikt
        // nadat de HTTP-request is afgerond.
        _ = Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sectorService = scope.ServiceProvider.GetRequiredService<SectorService>();
            await sectorService.EnrichSymbol(symbol);
        });

        return Created($"/api/v1/watchlist/{symbol}", new { data = request });
    }

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol)
    {
        var item = await _db.Watchlist
            .FirstOrDefaultAsync(w => w.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();
        item.IsActive  = false;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record AddWatchlistRequest(string Symbol, string? Exchange = null, string? Name = null, string? Sector = null);
