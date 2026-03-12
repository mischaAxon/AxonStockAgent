using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AxonStockAgent.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db) => _db = db;

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    /// <summary>GET /api/v1/favorites — lijst van favoriete symbolen voor de ingelogde user</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        var symbols = await _db.Favorites
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Symbol)
            .Select(f => f.Symbol)
            .ToListAsync();

        return Ok(new { data = symbols });
    }

    /// <summary>POST /api/v1/favorites/{symbol} — toggle: voeg toe of verwijder</summary>
    [HttpPost("{symbol}")]
    public async Task<IActionResult> Toggle(string symbol)
    {
        var userId = GetUserId();
        var upperSymbol = symbol.ToUpper();

        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Symbol == upperSymbol);

        if (existing != null)
        {
            _db.Favorites.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { data = new { symbol = upperSymbol, isFavorite = false } });
        }

        _db.Favorites.Add(new FavoriteEntity
        {
            UserId    = userId,
            Symbol    = upperSymbol,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok(new { data = new { symbol = upperSymbol, isFavorite = true } });
    }
}
