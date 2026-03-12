namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een symbool geïmporteerd van een exchange-listing.
/// Los van WatchlistItem — dit zijn alle bekende symbolen per beurs.
/// </summary>
public class MarketSymbolEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string? Name { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? SymbolType { get; set; } // "Common Stock", "ETF", etc.
    public string? Logo { get; set; }
    public long? MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
