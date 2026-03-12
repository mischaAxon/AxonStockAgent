using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly QuoteCacheService _quoteCache;

    public QuotesController(QuoteCacheService quoteCache) => _quoteCache = quoteCache;

    /// <summary>
    /// Haal quotes op voor meerdere symbolen tegelijk (max 50 per call).
    /// Quotes worden 30 seconden gecacht.
    /// </summary>
    [HttpGet("batch")]
    public async Task<IActionResult> GetBatchQuotes([FromQuery] string symbols)
    {
        if (string.IsNullOrWhiteSpace(symbols))
            return Ok(new { data = new Dictionary<string, object>() });

        var symbolList = symbols
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpper())
            .Distinct()
            .Take(50)
            .ToArray();

        var results = await _quoteCache.GetBatchQuotes(symbolList);

        return Ok(new { data = results });
    }

    /// <summary>
    /// Haal een enkele quote op (met cache).
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetQuote(string symbol)
    {
        var quote = await _quoteCache.GetQuote(symbol.ToUpper());
        if (quote == null)
            return NotFound(new { error = $"Geen quote beschikbaar voor {symbol}" });

        return Ok(new { data = quote });
    }
}
