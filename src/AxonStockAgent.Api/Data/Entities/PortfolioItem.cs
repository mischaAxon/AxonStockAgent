namespace AxonStockAgent.Api.Data.Entities;

public class PortfolioItem
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public int Shares { get; set; }
    public double? AvgBuyPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
