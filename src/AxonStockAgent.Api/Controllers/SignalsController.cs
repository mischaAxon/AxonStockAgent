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
}
