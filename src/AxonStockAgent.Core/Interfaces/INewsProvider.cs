namespace AxonStockAgent.Core.Interfaces;

public record NewsArticle(
    string Headline,
    string Summary,
    string Url,
    string Symbol,
    double SentimentScore,
    DateTime PublishedAt,
    string Source
);

public interface INewsProvider
{
    string Name { get; }
    Task<NewsArticle[]> GetNews(string? symbol = null, int limit = 20);
    Task<double> GetSentimentScore(string symbol, int days = 7);
}
