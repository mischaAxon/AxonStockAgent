namespace AxonStockAgent.Api.Data.Entities;

/// <summary>
/// Een beursindex (bijv. AEX, S&P 500) die gevolgd wordt.
/// </summary>
public class MarketIndexEntity
{
    public int Id { get; set; }
    /// <summary>EODHD symboolcode, bijv. "AEX.INDX", "GSPC.INDX"</summary>
    public string IndexSymbol { get; set; } = "";
    /// <summary>Weergavenaam, bijv. "AEX 25", "S&P 500"</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>Gekoppelde exchange code, bijv. "AS", "US"</summary>
    public string ExchangeCode { get; set; } = "";
    public string Country { get; set; } = "";
    public int SymbolCount { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastImportAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
