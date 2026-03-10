using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/fundamentals")]
[Authorize]
public class FundamentalsController : ControllerBase
{
    private readonly FundamentalsService _fundamentals;

    public FundamentalsController(FundamentalsService fundamentals)
    {
        _fundamentals = fundamentals;
    }

    /// <summary>Haal alle fundamentals op voor een symbool (cached, 24h TTL).</summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetFundamentals(string symbol, [FromQuery] bool refresh = false)
    {
        var data = await _fundamentals.GetFundamentals(symbol.ToUpperInvariant(), refresh);
        if (data == null) return NotFound(new { error = $"Geen fundamentals gevonden voor {symbol}" });
        return Ok(new { data });
    }

    /// <summary>Haal insider transactions op voor een symbool.</summary>
    [HttpGet("{symbol}/insiders")]
    public async Task<IActionResult> GetInsiders(string symbol, [FromQuery] bool refresh = false)
    {
        var data = await _fundamentals.GetInsiderTransactions(symbol.ToUpperInvariant(), refresh);
        return Ok(new { data });
    }

    /// <summary>Admin: vernieuw fundamentals voor alle watchlist items.</summary>
    [HttpPost("refresh-all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> RefreshAll()
    {
        var count = await _fundamentals.RefreshAllWatchlistFundamentals();
        return Ok(new { data = new { refreshed = count } });
    }
}
