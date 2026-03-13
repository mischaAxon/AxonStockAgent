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

        // Primair: MarketSymbols (alle index-leden met sector info)
        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .Select(m => new { m.Symbol, m.Sector })
            .ToListAsync();

        // Fallback: Watchlist als MarketSymbols leeg is
        if (symbols.Count == 0)
        {
            symbols = await _db.Watchlist
                .Where(w => w.IsActive)
                .Select(w => new { w.Symbol, w.Sector })
                .ToListAsync();
        }

        if (symbols.Count == 0)
        {
            _logger.LogInformation("No symbols found to fetch news for (MarketSymbols and Watchlist both empty)");
            return;
        }

        _logger.LogInformation("Fetching news for {Count} symbols", symbols.Count);

        var since = DateTime.UtcNow.AddHours(-24);
        var existingHeadlines = (await _db.NewsArticles
            .Where(n => n.FetchedAt >= since)
            .Select(n => n.Headline)
            .ToListAsync()).ToHashSet();

        int saved = 0;
        foreach (var provider in newsProviders)
        {
            // Per-symbol company news — met rate limiting (max 2 per seconde)
            int fetchedForProvider = 0;
            foreach (var item in symbols)
            {
                try
                {
                    var articles = await provider.GetNews(item.Symbol, limit: 5);
                    foreach (var article in articles)
                    {
                        if (existingHeadlines.Contains(article.Headline))
                            continue;

                        var entity = new NewsArticleEntity
                        {
                            Source = provider.Name,
                            Headline = article.Headline,
                            Summary = article.Summary,
                            Url = article.Url,
                            Symbol = item.Symbol,
                            Sector = item.Sector,
                            SentimentScore = article.SentimentScore,
                            PublishedAt = article.PublishedAt,
                            FetchedAt = DateTime.UtcNow
                        };

                        _db.NewsArticles.Add(entity);
                        existingHeadlines.Add(article.Headline);
                        saved++;
                    }

                    fetchedForProvider++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch news for {Symbol} from {Provider}", item.Symbol, provider.Name);
                }

                // Rate limiting: 500ms pauze per symbool
                await Task.Delay(500);
            }

            _logger.LogInformation("Fetched news for {Count} symbols from {Provider}", fetchedForProvider, provider.Name);

            // Algemeen marktnieuws (geen sector-filter)
            try
            {
                var generalArticles = await provider.GetNews(symbol: null, limit: 30);
                foreach (var article in generalArticles)
                {
                    if (existingHeadlines.Contains(article.Headline))
                        continue;

                    var entity = new NewsArticleEntity
                    {
                        Source = provider.Name,
                        Headline = article.Headline,
                        Summary = article.Summary,
                        Url = article.Url,
                        Symbol = null,
                        Sector = null,
                        SentimentScore = article.SentimentScore,
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
                _logger.LogWarning(ex, "Failed to fetch general news from {Provider}", provider.Name);
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

    /// <summary>
    /// Update sector-veld voor alle artikelen die een symbool hebben maar geen sector.
    /// </summary>
    public async Task<int> BackfillSectors()
    {
        var articles = await _db.NewsArticles
            .Where(n => n.Symbol != null && n.Sector == null)
            .ToListAsync();

        if (articles.Count == 0) return 0;

        var symbolSectors = await _db.MarketSymbols
            .Where(m => m.Sector != null)
            .ToDictionaryAsync(m => m.Symbol, m => m.Sector!);

        int updated = 0;
        foreach (var article in articles)
        {
            if (article.Symbol != null && symbolSectors.TryGetValue(article.Symbol, out var sector))
            {
                article.Sector = sector;
                updated++;
            }
        }

        if (updated > 0)
            await _db.SaveChangesAsync();

        _logger.LogInformation("Backfilled sector for {Updated}/{Total} articles", updated, articles.Count);
        return updated;
    }

    public async Task<List<TrendingSymbolDto>> GetTrendingSymbols(int limit = 10)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var rows = await _db.NewsArticles
            .Where(n => n.PublishedAt >= since && n.Symbol != null)
            .GroupBy(n => n.Symbol!)
            .Select(g => new { Symbol = g.Key, Count = g.Count(), AvgSentiment = g.Average(x => x.SentimentScore) })
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToListAsync();

        return rows.Select(r => new TrendingSymbolDto(r.Symbol, r.Count, r.AvgSentiment)).ToList();
    }
}
