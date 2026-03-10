using System;

namespace AxonStockAgent.Api.Data.Entities;

public class InsiderTransactionEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public long Shares { get; set; }
    public double PricePerShare { get; set; }
    public double TotalValue { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
