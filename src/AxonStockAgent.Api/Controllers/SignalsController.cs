using AxonStockAgent.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class SignalsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SignalsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? symbol = null,
        [FromQuery] string? verdict = null,
        [FromQuery] DateTime? since = null)
    {
        var query = _db.Signals.AsQueryable();

        if (!string.IsNullOrEmpty(symbol))
            query = query.Where(s => s.Symbol == symbol.ToUpper());

        if (!string.IsNullOrEmpty(verdict))
            query = query.Where(s => s.FinalVerdict == verdict.ToUpper());

        if (since.HasValue)
            query = query.Where(s => s.CreatedAt >= since.Value.ToUniversalTime());

        var total = await query.CountAsync();

        var signals = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return Ok(new
        {
            data = signals,
            meta = new { page, limit, total }
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var signal = await _db.Signals.FindAsync(id);
        if (signal == null) return NotFound();
        return Ok(new { data = signal });
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery] int count = 10)
    {
        var signals = await _db.Signals
            .Where(s => s.FinalVerdict != "SKIP")
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync();

        return Ok(new { data = signals });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var totalSignals = await _db.Signals.CountAsync();
        var todaySignals = await _db.Signals.CountAsync(s => s.CreatedAt >= today);
        var weekSignals = await _db.Signals.CountAsync(s => s.CreatedAt >= weekAgo);

        var verdictCounts = await _db.Signals
            .Where(s => s.CreatedAt >= weekAgo)
            .GroupBy(s => s.FinalVerdict)
            .Select(g => new { verdict = g.Key, count = g.Count() })
            .ToListAsync();

        return Ok(new { data = new { totalSignals, todaySignals, weekSignals, verdictCounts } });
    }

    [HttpGet("latest-per-symbol")]
    public async Task<IActionResult> GetLatestPerSymbol([FromQuery] int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Min(days, 90));

        // EF Core kan GroupBy+First() niet naar SQL vertalen — gebruik DISTINCT ON (PostgreSQL)
        var latestSignals = await _db.Signals
            .FromSqlInterpolated($@"
                SELECT DISTINCT ON (symbol) *
                FROM signals
                WHERE created_at >= {since}
                ORDER BY symbol, created_at DESC")
            .AsNoTracking()
            .ToListAsync();

        return Ok(new { data = latestSignals.Select(s => new
        {
            s.Symbol,
            s.FinalVerdict,
            s.FinalScore,
            s.Direction,
            s.CreatedAt,
            s.TrendStatus,
            s.MomentumStatus,
            s.VolatilityStatus,
            s.VolumeStatus,
            s.TechScore,
            s.SentimentScore,
            s.ClaudeConfidence,
            s.ClaudeDirection
        })});
    }

    [HttpGet("accuracy")]
    public async Task<IActionResult> GetAccuracy([FromQuery] int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var signals = await _db.Signals
            .Where(s => s.CreatedAt >= since && s.OutcomeCorrect.HasValue)
            .ToListAsync();

        if (signals.Count == 0)
            return Ok(new { data = new { totalTracked = 0, message = "Nog geen outcome data beschikbaar" } });

        var correct   = signals.Count(s => s.OutcomeCorrect == true);
        var incorrect = signals.Count(s => s.OutcomeCorrect == false);
        var accuracy  = (double)correct / signals.Count * 100;

        var byVerdict = signals
            .GroupBy(s => s.FinalVerdict)
            .Select(g => new
            {
                verdict      = g.Key,
                total        = g.Count(),
                correct      = g.Count(s => s.OutcomeCorrect == true),
                accuracy     = g.Count() > 0 ? (double)g.Count(s => s.OutcomeCorrect == true) / g.Count() * 100 : 0,
                avgReturn1d  = g.Where(s => s.ReturnPct1d.HasValue).Select(s => s.ReturnPct1d!.Value).DefaultIfEmpty(0).Average(),
                avgReturn5d  = g.Where(s => s.ReturnPct5d.HasValue).Select(s => s.ReturnPct5d!.Value).DefaultIfEmpty(0).Average(),
                avgReturn20d = g.Where(s => s.ReturnPct20d.HasValue).Select(s => s.ReturnPct20d!.Value).DefaultIfEmpty(0).Average()
            })
            .ToList();

        return Ok(new
        {
            data = new
            {
                totalTracked = signals.Count,
                correct,
                incorrect,
                accuracyPct  = Math.Round(accuracy, 1),
                byVerdict,
                periodDays   = days
            }
        });
    }
}
