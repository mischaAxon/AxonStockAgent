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
    /// Leest uit TrackedExchanges (admin-geconfigureerd) + MarketSymbols.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exchanges = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .GroupBy(m => new { m.Exchange, m.Country })
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
    /// Retourneert alle symbolen voor een specifieke beurs.
    /// </summary>
    [HttpGet("{exchange}/symbols")]
    public async Task<IActionResult> GetSymbols(string exchange)
    {
        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive && m.Exchange == exchange)
            .OrderBy(m => m.Symbol)
            .Select(m => new
            {
                m.Symbol,
                m.Name,
                m.Sector,
                m.Industry,
                m.Country,
                m.Logo,
                m.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }

    /// <summary>
    /// Retourneert ALLE actieve symbolen uit MarketSymbols.
    /// Optioneel filteren op country of exchange.
    /// </summary>
    [HttpGet("all-symbols")]
    public async Task<IActionResult> GetAllSymbols([FromQuery] string? country = null, [FromQuery] string? exchange = null)
    {
        var query = _db.MarketSymbols.Where(m => m.IsActive);

        if (!string.IsNullOrEmpty(country))
            query = query.Where(m => m.Country == country);
        if (!string.IsNullOrEmpty(exchange))
            query = query.Where(m => m.Exchange == exchange);

        var symbols = await query
            .OrderBy(m => m.Country)
            .ThenBy(m => m.Exchange)
            .ThenBy(m => m.Symbol)
            .Select(m => new
            {
                m.Symbol,
                m.Name,
                Exchange = m.Exchange,
                m.Sector,
                m.Industry,
                m.Country,
                m.Logo,
                m.MarketCap
            })
            .ToListAsync();

        return Ok(new { data = symbols });
    }

    /// <summary>
    /// Retourneert alle actieve indexen met hun componenten-symbolen.
    /// Dit is wat de frontend gebruikt om het Markets-scherm per index te groeperen.
    /// </summary>
    [HttpGet("indices-with-symbols")]
    public async Task<IActionResult> GetIndicesWithSymbols()
    {
        var indices = await _db.MarketIndices
            .Where(i => i.IsEnabled)
            .OrderBy(i => i.Country)
            .ThenBy(i => i.DisplayName)
            .Select(i => new
            {
                i.Id,
                i.IndexSymbol,
                i.DisplayName,
                i.ExchangeCode,
                i.Country,
                i.SymbolCount,
                Symbols = _db.IndexMemberships
                    .Where(m => m.MarketIndexId == i.Id)
                    .Join(_db.MarketSymbols.Where(ms => ms.IsActive),
                        m => m.Symbol,
                        ms => ms.Symbol,
                        (m, ms) => new
                        {
                            ms.Symbol,
                            ms.Name,
                            ms.Exchange,
                            ms.Sector,
                            ms.Industry,
                            ms.Country,
                            ms.Logo,
                            ms.MarketCap
                        })
                    .OrderBy(s => s.Symbol)
                    .ToList()
            })
            .ToListAsync();

        return Ok(new { data = indices });
    }
}
