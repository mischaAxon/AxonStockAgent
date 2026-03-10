using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;

namespace AxonStockAgent.Api.Services;

public record NewsArticleDto(
    int Id,
    string Source,
    string Headline,
    string? Summary,
    string? Url,
    string? Symbol,
    string? Sector,
    double SentimentScore,
    DateTime PublishedAt,
    DateTime FetchedAt
);

public record SectorSentimentDto(
    string Sector,
    double AvgSentiment,
    int ArticleCount,
    DateTime CalculatedAt
);

public record TrendingSymbolDto(string Symbol, int ArticleCount, double AvgSentiment);

public class NewsService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providerManager;
    private readonly ILogger<NewsService> _logger;

    public NewsService(AppDbContext db, ProviderManager providerManager, ILogger<NewsService> logger)
    {
        _db = db;
        _providerManager = providerManager;
        _logger = logger;
    }

    public async Task FetchLatestNews()
    {
        var newsProviders = await _providerManager.GetAllNewsProviders();
        if (newsProviders.Length == 0)
        {
            _logger.LogWarning("No active news providers found");
            return;
        }

        var symbols = await _db.Watchlist
            .Select(w => new { w.Symbol, w.Sector })
            .ToListAsync();

        if (symbols.Count == 0)
        {
            _logger.LogInformation("No watchlist symbols to fetch news for");
            return;
        }

        var since = DateTime.UtcNow.AddHours(-24);
        var existingHeadlinesList = await _db.NewsArticles
            .Where(n => n.FetchedAt >= since)
            .Select(n => n.Headline)
            .ToListAsync();
        var existingHeadlines = existingHeadlinesList.ToHashSet();

        int saved = 0;
        foreach (var provider in newsProviders)
        {
            foreach (var item in symbols)
            {
                try
                {
                    var articles = await provider.GetNews(item.Symbol);
                    foreach (var article in articles)
                    {
                        if (existingHeadlines.Contains(article.Headline))
                            continue;

                        double sentiment = article.SentimentScore;
                        if (sentiment == 0)
                        {
                            try { sentiment = await provider.GetSentimentScore(item.Symbol); }
                            catch { /* ignore */ }
                        }

                        var entity = new NewsArticleEntity
                        {
                            Source = provider.Name,
                            Headline = article.Headline,
                            Summary = article.Summary,
                            Url = article.Url,
                            Symbol = item.Symbol,
                            Sector = item.Sector,
                            SentimentScore = sentiment,
                            PublishedAt = article.PublishedAt,
                            FetchedAt = DateTime.UtcNow
                        };

                        _db.NewsArticles.Add(entity);
                        existingHeadlines.Add(article.Headline);
                        saved++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch news for {Symbol} from {Provider}", item.Symbol, provider.Name);
                }
            }
        }

        if (saved > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Saved {Count} new articles", saved);
        }
    }

    public async Task CalculateSectorSentiment()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var periodEnd = DateTime.UtcNow;

        var grouped = await _db.NewsArticles
            .Where(n => n.PublishedAt >= since && n.Sector != null)
            .GroupBy(n => n.Sector!)
            .Select(g => new
            {
                Sector = g.Key,
                AvgSentiment = g.Average(x => x.SentimentScore),
                ArticleCount = g.Count()
            })
            .ToListAsync();

        foreach (var item in grouped)
        {
            var entity = new SectorSentimentEntity
            {
                Sector = item.Sector,
                AvgSentiment = item.AvgSentiment,
                ArticleCount = item.ArticleCount,
                PeriodStart = since,
                PeriodEnd = periodEnd,
                CalculatedAt = DateTime.UtcNow
            };
            _db.SectorSentiment.Add(entity);
        }

        if (grouped.Count > 0)
            await _db.SaveChangesAsync();
    }

    public async Task<List<NewsArticleDto>> GetLatestNews(int limit = 20)
    {
        return await _db.NewsArticles
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .Select(n => new NewsArticleDto(n.Id, n.Source, n.Headline, n.Summary, n.Url, n.Symbol, n.Sector, n.SentimentScore, n.PublishedAt, n.FetchedAt))
            .ToListAsync();
    }

    public async Task<List<NewsArticleDto>> GetNewsBySymbol(string symbol, int limit = 10)
    {
        return await _db.NewsArticles
            .Where(n => n.Symbol == symbol.ToUpperInvariant())
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .Select(n => new NewsArticleDto(n.Id, n.Source, n.Headline, n.Summary, n.Url, n.Symbol, n.Sector, n.SentimentScore, n.PublishedAt, n.FetchedAt))
            .ToListAsync();
    }

    public async Task<List<SectorSentimentDto>> GetSectorSentiment()
    {
        var latest = await _db.SectorSentiment
            .GroupBy(s => s.Sector)
            .Select(g => g.OrderByDescending(x => x.CalculatedAt).First())
            .ToListAsync();

        return latest.Select(s => new SectorSentimentDto(s.Sector, s.AvgSentiment, s.ArticleCount, s.CalculatedAt)).ToList();
    }

    public async Task<List<TrendingSymbolDto>> GetTrendingSymbols(int limit = 10)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        return await _db.NewsArticles
            .Where(n => n.PublishedAt >= since && n.Symbol != null)
            .GroupBy(n => n.Symbol!)
            .Select(g => new TrendingSymbolDto(g.Key, g.Count(), g.Average(x => x.SentimentScore)))
            .OrderByDescending(x => x.ArticleCount)
            .Take(limit)
            .ToListAsync();
    }
}
