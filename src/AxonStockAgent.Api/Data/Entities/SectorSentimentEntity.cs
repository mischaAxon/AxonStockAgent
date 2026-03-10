using System;

namespace AxonStockAgent.Api.Data.Entities;

public class SectorSentimentEntity
{
    public int Id { get; set; }
    public string Sector { get; set; } = string.Empty;
    public double AvgSentiment { get; set; }
    public int ArticleCount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
