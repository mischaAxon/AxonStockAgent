using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class FundamentalsService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<FundamentalsService> _logger;

    // Cache TTL: data ouder dan dit wordt opnieuw opgehaald
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public FundamentalsService(AppDbContext db, ProviderManager providers, ILogger<FundamentalsService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Haalt fundamentals op voor een symbool. Gebruikt cache, refresht als ouder dan 24 uur.
    /// </summary>
    public async Task<CompanyFundamentalsEntity?> GetFundamentals(string symbol, bool forceRefresh = false)
    {
        var cached = await _db.CompanyFundamentals.FirstOrDefaultAsync(f => f.Symbol == symbol);

        if (cached != null && !forceRefresh && (DateTime.UtcNow - cached.FetchedAt) < CacheTtl)
            return cached;

        var provider = await _providers.GetFundamentalsProvider();
        if (provider == null)
        {
            _logger.LogWarning("Geen fundamentals provider beschikbaar");
            return cached; // geef stale cache terug als er geen provider is
        }

        try
        {
            // Haal alle data parallel op
            var metricsTask = provider.GetFinancialMetrics(symbol);
            var ratingsTask = provider.GetAnalystRatings(symbol);
            var targetTask  = provider.GetPriceTarget(symbol);

            await Task.WhenAll(metricsTask, ratingsTask, targetTask);

            var metrics = metricsTask.Result;
            var ratings = ratingsTask.Result;
            var target  = targetTask.Result;

            if (metrics == null && ratings == null && target == null)
                return cached;

            if (cached == null)
            {
                cached = new CompanyFundamentalsEntity { Symbol = symbol };
                _db.CompanyFundamentals.Add(cached);
            }

            // Update metrics
            if (metrics != null)
            {
                cached.PeRatio           = metrics.PeRatio;
                cached.ForwardPe         = metrics.ForwardPe;
                cached.PbRatio           = metrics.PbRatio;
                cached.PsRatio           = metrics.PsRatio;
                cached.EvToEbitda        = metrics.EvToEbitda;
                cached.ProfitMargin      = metrics.ProfitMargin;
                cached.OperatingMargin   = metrics.OperatingMargin;
                cached.ReturnOnEquity    = metrics.ReturnOnEquity;
                cached.ReturnOnAssets    = metrics.ReturnOnAssets;
                cached.RevenueGrowthYoy  = metrics.RevenueGrowthYoy;
                cached.EarningsGrowthYoy = metrics.EarningsGrowthYoy;
                cached.DebtToEquity      = metrics.DebtToEquity;
                cached.CurrentRatio      = metrics.CurrentRatio;
                cached.QuickRatio        = metrics.QuickRatio;
                cached.DividendYield     = metrics.DividendYield;
                cached.PayoutRatio       = metrics.PayoutRatio;
                cached.MarketCap         = metrics.MarketCap;
                cached.Revenue           = metrics.Revenue;
                cached.NetIncome         = metrics.NetIncome;
                cached.SharesOutstanding = metrics.SharesOutstanding;
            }

            // Update analyst data
            if (ratings != null)
            {
                cached.AnalystBuy        = ratings.Buy;
                cached.AnalystHold       = ratings.Hold;
                cached.AnalystSell       = ratings.Sell;
                cached.AnalystStrongBuy  = ratings.StrongBuy;
                cached.AnalystStrongSell = ratings.StrongSell;
            }

            if (target != null)
            {
                cached.TargetPriceHigh   = target.TargetHigh;
                cached.TargetPriceLow    = target.TargetLow;
                cached.TargetPriceMean   = target.TargetMean;
                cached.TargetPriceMedian = target.TargetMedian;
            }

            cached.FetchedAt = DateTime.UtcNow;
            cached.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("Fundamentals bijgewerkt voor {Symbol}", symbol);
            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fundamentals ophalen mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return cached;
        }
    }

    /// <summary>
    /// Haalt insider transactions op. Slaat nieuwe transacties op, deduplicatie via unique constraint.
    /// </summary>
    public async Task<InsiderTransactionEntity[]> GetInsiderTransactions(string symbol, bool forceRefresh = false)
    {
        var cached = await _db.InsiderTransactions
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var mostRecent = cached.FirstOrDefault()?.FetchedAt ?? DateTime.MinValue;
        if (cached.Any() && !forceRefresh && (DateTime.UtcNow - mostRecent) < CacheTtl)
            return cached.ToArray();

        var provider = await _providers.GetFundamentalsProvider();
        if (provider == null) return cached.ToArray();

        try
        {
            var transactions = await provider.GetInsiderTransactions(symbol, months: 6);

            foreach (var tx in transactions)
            {
                var exists = await _db.InsiderTransactions.AnyAsync(t =>
                    t.Symbol == symbol && t.Name == tx.Name && t.TransactionDate == tx.Date);

                if (!exists)
                {
                    _db.InsiderTransactions.Add(new InsiderTransactionEntity
                    {
                        Symbol          = symbol,
                        Name            = tx.Name,
                        Relation        = tx.Relation,
                        TransactionType = tx.TransactionType,
                        TransactionDate = tx.Date,
                        Shares          = tx.Shares,
                        PricePerShare   = tx.PricePerShare,
                        TotalValue      = tx.TotalValue,
                        FetchedAt       = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("{Count} insider transacties opgeslagen voor {Symbol}", transactions.Length, symbol);

            return await _db.InsiderTransactions
                .Where(t => t.Symbol == symbol)
                .OrderByDescending(t => t.TransactionDate)
                .ToArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Insider transactions mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return cached.ToArray();
        }
    }

    /// <summary>
    /// Vernieuwt fundamentals voor alle actieve watchlist items.
    /// Rate-limited: 2 seconden pauze tussen symbolen.
    /// </summary>
    public async Task<int> RefreshAllWatchlistFundamentals()
    {
        var symbols = await _db.Watchlist
            .Where(w => w.IsActive)
            .Select(w => w.Symbol)
            .ToListAsync();

        int refreshed = 0;
        foreach (var symbol in symbols)
        {
            var result = await GetFundamentals(symbol, forceRefresh: true);
            if (result != null) refreshed++;
            await Task.Delay(2000); // rate limiting
        }
        return refreshed;
    }

    /// <summary>
    /// Vernieuwt fundamentals voor alle actieve MarketSymbols.
    /// Rate-limited: 2 seconden pauze tussen symbolen (EODHD rate limit).
    /// Retourneert (total, success, failed).
    /// </summary>
    public async Task<(int total, int success, int failed)> RefreshAllMarketSymbolsFundamentals(
        IProgress<(int current, int total, string symbol)>? progress = null)
    {
        var symbols = await _db.MarketSymbols
            .Where(m => m.IsActive)
            .Select(m => m.Symbol)
            .ToListAsync();

        int success = 0, failed = 0;

        for (int i = 0; i < symbols.Count; i++)
        {
            var symbol = symbols[i];
            progress?.Report((i + 1, symbols.Count, symbol));

            try
            {
                var result = await GetFundamentals(symbol, forceRefresh: true);
                if (result != null)
                    success++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Fundamentals refresh mislukt voor {Symbol}: {Message}", symbol, ex.Message);
                failed++;
            }

            // Rate limiting: EODHD doet 3 calls per symbool (metrics + ratings + target)
            // Bij 90 calls/min max → 30 symbolen/min → 2 sec per symbool
            await Task.Delay(2000);
        }

        _logger.LogInformation(
            "Fundamentals bulk refresh voltooid: {Total} symbolen, {Success} succesvol, {Failed} mislukt",
            symbols.Count, success, failed);

        return (symbols.Count, success, failed);
    }
}
