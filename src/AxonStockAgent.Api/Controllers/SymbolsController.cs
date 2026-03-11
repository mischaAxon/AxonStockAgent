using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/symbols")]
public class SymbolsController : ControllerBase
{
    private readonly ProviderManager _providers;

    public SymbolsController(ProviderManager providers)
    {
        _providers = providers;
    }

    /// <summary>Zoek aandelen op naam of ticker via actieve providers.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(new { data = Array.Empty<object>() });

        var results = await _providers.SearchSymbols(q.Trim());
        return Ok(new { data = results });
    }
}
