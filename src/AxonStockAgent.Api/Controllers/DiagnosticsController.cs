using AxonStockAgent.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[ApiController]
[Route("api/v1/diagnostics")]
[Authorize(Roles = "admin")]
public class DiagnosticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DiagnosticsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Claude API success rate en faalredenen over de afgelopen N dagen.
    /// </summary>
    [HttpGet("claude/stats")]
    public async Task<IActionResult> GetClaudeStats([FromQuery] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var logs = await _db.ClaudeApiLogs
            .Where(l => l.CreatedAt >= since)
            .ToListAsync();

        if (logs.Count == 0)
            return Ok(new { message = "Geen Claude API logs gevonden", days });

        var totalCalls = logs.Count;
        var successCount = logs.Count(l => l.Status == "success");
        var successRate = (double)successCount / totalCalls;

        var byStatus = logs
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Pct = (double)g.Count() / totalCalls })
            .OrderByDescending(x => x.Count)
            .ToList();

        var bySymbol = logs
            .GroupBy(l => l.Symbol)
            .Select(g => new
            {
                Symbol = g.Key,
                Total = g.Count(),
                Success = g.Count(l => l.Status == "success"),
                SuccessRate = (double)g.Count(l => l.Status == "success") / g.Count(),
                AvgDurationMs = g.Average(l => l.DurationMs)
            })
            .OrderBy(x => x.SuccessRate)
            .ToList();

        var avgDuration = logs.Average(l => l.DurationMs);

        var recentErrors = logs
            .Where(l => l.Status != "success")
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .Select(l => new
            {
                l.Symbol,
                l.Status,
                l.ErrorMessage,
                l.RawResponseSnippet,
                l.HttpStatusCode,
                l.CreatedAt,
                l.DurationMs
            })
            .ToList();

        return Ok(new
        {
            period = new { days, since, until = DateTime.UtcNow },
            summary = new
            {
                totalCalls,
                successCount,
                failureCount = totalCalls - successCount,
                successRate = Math.Round(successRate, 4),
                avgDurationMs = Math.Round(avgDuration, 0)
            },
            byStatus,
            bySymbol,
            recentErrors
        });
    }
}
