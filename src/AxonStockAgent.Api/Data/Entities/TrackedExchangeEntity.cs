namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een beurs die de admin heeft ingeschakeld voor automatische symbol-import.
/// </summary>
public class TrackedExchangeEntity
{
    public int Id { get; set; }
    /// <summary>Exchange code zoals EODHD die gebruikt: "AS", "US", "XETRA", "LSE", etc.</summary>
    public string ExchangeCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    /// <summary>Aantal symbolen bij laatste import.</summary>
    public int SymbolCount { get; set; } = 0;
    public DateTime? LastImportAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
