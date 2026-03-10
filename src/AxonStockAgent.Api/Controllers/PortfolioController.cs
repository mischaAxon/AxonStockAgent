using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _db;

    public PortfolioController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Portfolio.OrderBy(p => p.Symbol).ToListAsync();
        return Ok(new { data = items });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertPortfolioRequest request)
    {
        var existing = await _db.Portfolio.FirstOrDefaultAsync(p => p.Symbol == request.Symbol.ToUpper());

        if (existing != null)
        {
            existing.Shares = request.Shares;
            existing.AvgBuyPrice = request.AvgBuyPrice;
            existing.Notes = request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Portfolio.Add(new PortfolioItem
            {
                Symbol = request.Symbol.ToUpper(),
                Shares = request.Shares,
                AvgBuyPrice = request.AvgBuyPrice,
                Notes = request.Notes,
                AddedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { data = request });
    }

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol)
    {
        var item = await _db.Portfolio.FirstOrDefaultAsync(p => p.Symbol == symbol.ToUpper());
        if (item == null) return NotFound();
        _db.Portfolio.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record UpsertPortfolioRequest(string Symbol, int Shares, double? AvgBuyPrice = null, string? Notes = null);
