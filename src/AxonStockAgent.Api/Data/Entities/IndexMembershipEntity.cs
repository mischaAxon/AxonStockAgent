namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Koppeling tussen een MarketSymbol en een MarketIndex.
/// Een symbool kan in meerdere indexen zitten.
/// </summary>
public class IndexMembershipEntity
{
    public int Id { get; set; }
    public int MarketIndexId { get; set; }
    public MarketIndexEntity MarketIndex { get; set; } = null!;
    /// <summary>Volledig symbool zoals in MarketSymbols, bijv. "ASML.AS"</summary>
    public string Symbol { get; set; } = "";
    public string? Name { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
