namespace AxonStockAgent.Api.Data.Entities;

public class DividendEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime ExDate { get; set; }
    public DateTime? PayDate { get; set; }
    public double Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime FetchedAt { get; set; }
}
