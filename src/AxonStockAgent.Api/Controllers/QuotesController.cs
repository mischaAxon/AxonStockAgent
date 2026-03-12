using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly ProviderManager _providers;

    public QuotesController(ProviderManager providers) => _providers = providers;

    /// <summary>
    /// Haal quotes op voor meerdere symbolen tegelijk.
    /// Returns current price, change %, previous close.
    /// Max 50 symbolen per call.
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

        var results = new Dictionary<string, object>();

        var tasks = symbolList.Select(async symbol =>
        {
            try
            {
                var quote = await _providers.GetQuote(symbol);
                if (quote != null)
                    return (symbol, (object?)quote);
            }
            catch { /* skip failed quotes */ }
            return (symbol, (object?)null);
        });

        foreach (var (symbol, quote) in await Task.WhenAll(tasks))
        {
            if (quote != null)
                results[symbol] = quote;
        }

        return Ok(new { data = results });
    }
}
