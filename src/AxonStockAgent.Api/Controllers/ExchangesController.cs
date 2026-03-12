using AxonStockAgent.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ExchangesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExchangesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Retourneert alle beurzen met hun symbooltellingen, gegroepeerd per land.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exchanges = await _db.Watchlist
            .Where(w => w.IsActive && w.Exchange != null)
            .GroupBy(w => new { w.Exchange, w.Country })
            .Select(g => new
            {
                Exchange = g.Key.Exchange,
                Country = g.Key.Country ?? "XX",
                SymbolCount = g.Count()
            })
            .OrderBy(e => e.Country)
            .ThenBy(e => e.Exchange)
            .ToListAsync();

        return Ok(new { data = exchanges });
    }

    /// <summary>
    /// Retourneert alle symbolen voor een specifieke beurs, met basisinfo.
    /// </summary>
    [HttpGet("{exchange}/symbols")]
    public async Task<IActionResult> GetSymbols(string exchange)
    {
        var symbols = await _db.Watchlist
            .Where(w => w.IsActive && w.Exchange == exchange)
            .OrderBy(w => w.Symbol)
            .Select(w => new
            {
                w.Symbol,
                w.Name,
                w.Sector,
                w.Industry,
                w.Country,
                w.Logo,
                w.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }

    /// <summary>
    /// Retourneert ALLE actieve symbolen met basisinfo, gegroepeerd per exchange.
    /// Optioneel filteren op country.
    /// </summary>
    [HttpGet("all-symbols")]
    public async Task<IActionResult> GetAllSymbols([FromQuery] string? country = null)
    {
        var query = _db.Watchlist.Where(w => w.IsActive);

        if (!string.IsNullOrEmpty(country))
            query = query.Where(w => w.Country == country);

        var symbols = await query
            .OrderBy(w => w.Country)
            .ThenBy(w => w.Exchange)
            .ThenBy(w => w.Symbol)
            .Select(w => new
            {
                w.Symbol,
                w.Name,
                w.Exchange,
                w.Sector,
                w.Industry,
                w.Country,
                w.Logo,
                w.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }
}
