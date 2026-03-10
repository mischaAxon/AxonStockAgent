using System;

namespace AxonStockAgent.Api.Data.Entities;

public class NewsArticleEntity
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Url { get; set; }
    public string? Symbol { get; set; }
    public string? Sector { get; set; }
    public double SentimentScore { get; set; } = 0;
    public DateTime PublishedAt { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
