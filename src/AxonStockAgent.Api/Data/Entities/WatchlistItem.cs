namespace AxonStockAgent.Api.Data.Entities;

public class WatchlistItem
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string? Exchange { get; set; }
    public string? Name { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public long? MarketCap { get; set; }
    public string? Logo { get; set; }
    public string? WebUrl { get; set; }
    /// <summary>"finnhub" | "eodhd" | "manual" | null</summary>
    public string? SectorSource { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
