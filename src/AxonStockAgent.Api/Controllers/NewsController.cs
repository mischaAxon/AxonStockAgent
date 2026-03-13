using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AxonStockAgent.Api.Services;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/news")]
[Authorize]
public class NewsController : ControllerBase
{
    private readonly NewsService _newsService;

    public NewsController(NewsService newsService)
    {
        _newsService = newsService;
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery] int limit = 50)
        => Ok(await _newsService.GetLatestNews(Math.Clamp(limit, 1, 500)));

    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetBySymbol(string symbol, [FromQuery] int limit = 10)
        => Ok(await _newsService.GetNewsBySymbol(symbol, Math.Clamp(limit, 1, 50)));

    [HttpGet("sector-sentiment")]
    public async Task<IActionResult> GetSectorSentiment()
        => Ok(await _newsService.GetSectorSentiment());

    [HttpGet("trending")]
    public async Task<IActionResult> GetTrending()
        => Ok(await _newsService.GetTrendingSymbols());
}
