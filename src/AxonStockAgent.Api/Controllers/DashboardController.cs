using AxonStockAgent.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

/// <summary>
/// Aggregated dashboard data for the frontend
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = today.AddDays(-7);

        // Portfolio summary
        var portfolio = await _db.Portfolio.ToListAsync();
        var portfolioValue = portfolio.Sum(p => p.Shares * (p.AvgBuyPrice ?? 0));

        // Recent signals
        var recentSignals = await _db.Signals
            .Where(s => s.FinalVerdict != "SKIP")
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .Select(s => new
            {
                s.Id,
                s.Symbol,
                s.FinalVerdict,
                s.FinalScore,
                s.PriceAtSignal,
                s.ClaudeReasoning,
                s.CreatedAt
            })
            .ToListAsync();

        // Signal stats
        var weekBuys = await _db.Signals.CountAsync(s => s.FinalVerdict == "BUY" && s.CreatedAt >= weekAgo);
        var weekSells = await _db.Signals.CountAsync(s => s.FinalVerdict == "SELL" && s.CreatedAt >= weekAgo);
        var weekSqueezes = await _db.Signals.CountAsync(s => s.FinalVerdict == "SQUEEZE" && s.CreatedAt >= weekAgo);

        // Upcoming dividends
        var upcomingDividends = await _db.Dividends
            .Where(d => d.ExDate >= today && d.ExDate <= today.AddDays(30))
            .OrderBy(d => d.ExDate)
            .Take(5)
            .ToListAsync();

        return Ok(new
        {
            data = new
            {
                portfolioPositions = portfolio.Count,
                portfolioEstimatedValue = portfolioValue,
                signals = new { weekBuys, weekSells, weekSqueezes },
                recentSignals,
                upcomingDividends
            }
        });
    }
}
