using System.Text.Json;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Api.Providers;

/// <summary>
/// Finnhub implementatie van IMarketDataProvider, INewsProvider en IFundamentalsProvider.
/// Gratis tier: 60 calls/min. Rate limiter zorgt voor max ~55/min om marge te houden.
/// </summary>
public class FinnhubProvider : IMarketDataProvider, INewsProvider, IFundamentalsProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<FinnhubProvider> _logger;

    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastCall = DateTime.MinValue;
    private const int MinMsBetweenCalls = 1100; // ~55/min
    private const string BaseUrl = "https://finnhub.io/api/v1";

    public string Name => "finnhub";
    public bool SupportsRealtime => true;
    public string[] SupportedExchanges => ["US", "XAMS", "XETR", "XPAR", "XLON", "TO"];

    public FinnhubProvider(HttpClient http, string apiKey, ILogger<FinnhubProvider> logger)
    {
        _http = http;
        _apiKey = apiKey;
        _logger = logger;
    }

    private async Task RateLimit()
    {
        await _rateLimiter.WaitAsync();
        try
        {
            var elapsed = (DateTime.UtcNow - _lastCall).TotalMilliseconds;
            if (elapsed < MinMsBetweenCalls)
                await Task.Delay((int)(MinMsBetweenCalls - elapsed));
            _lastCall = DateTime.UtcNow;
        }
        finally { _rateLimiter.Release(); }
    }

    // ── IMarketDataProvider ────────────────────────────────────────────────────

    public async Task<Candle[]?> GetCandles(string symbol, string resolution, int count)
    {
        await RateLimit();
        long to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long from = resolution == "D"
            ? to - (count + 30) * 86400L
            : to - (long)count * 3600 * 4;

        var url = $"{BaseUrl}/stock/candle?symbol={symbol}&resolution={resolution}&from={from}&to={to}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("s", out var status) || status.GetString() != "ok")
                return null;

            var times  = root.GetProperty("t").EnumerateArray().Select(x => DateTimeOffset.FromUnixTimeSeconds(x.GetInt64()).UtcDateTime).ToArray();
            var opens  = root.GetProperty("o").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var highs  = root.GetProperty("h").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var lows   = root.GetProperty("l").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var closes = root.GetProperty("c").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var vols   = root.GetProperty("v").EnumerateArray().Select(x => x.GetInt64()).ToArray();

            return times.Select((t, i) => new Candle(t, opens[i], highs[i], lows[i], closes[i], vols[i])).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetCandles mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<string[]> GetSymbols(string exchange)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/symbol?exchange={exchange}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(s => s.GetProperty("symbol").GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetSymbols mislukt voor {Exchange}: {Message}", exchange, ex.Message);
            return Array.Empty<string>();
        }
    }

    // ── INewsProvider ──────────────────────────────────────────────────────────

    public async Task<NewsArticle[]> GetNews(string? symbol = null, int limit = 20)
    {
        await RateLimit();
        string url;
        if (symbol != null)
        {
            var to   = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
            url = $"{BaseUrl}/company-news?symbol={symbol}&from={from}&to={to}&token={_apiKey}";
        }
        else
        {
            url = $"{BaseUrl}/news?category=general&token={_apiKey}";
        }

        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Take(limit)
                .Select(a => new NewsArticle(
                    Headline:      a.TryGetProperty("headline", out var h)  ? h.GetString()  ?? "" : "",
                    Summary:       a.TryGetProperty("summary",  out var s)  ? s.GetString()  ?? "" : "",
                    Url:           a.TryGetProperty("url",      out var u)  ? u.GetString()  ?? "" : "",
                    Symbol:        symbol ?? "",
                    SentimentScore: 0,
                    PublishedAt:   a.TryGetProperty("datetime", out var dt) ? DateTimeOffset.FromUnixTimeSeconds(dt.GetInt64()).UtcDateTime : DateTime.UtcNow,
                    Source:        a.TryGetProperty("source",   out var src) ? src.GetString() ?? "" : ""
                ))
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetNews mislukt: {Message}", ex.Message);
            return Array.Empty<NewsArticle>();
        }
    }

    public async Task<double> GetSentimentScore(string symbol, int days = 7)
    {
        await RateLimit();
        var url = $"{BaseUrl}/news-sentiment?symbol={symbol}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("sentiment", out var sent))
            {
                var bullish = sent.TryGetProperty("bullishPercent", out var b)  ? b.GetDouble()  : 0.5;
                var bearish = sent.TryGetProperty("bearishPercent", out var be) ? be.GetDouble() : 0.5;
                return bullish - bearish; // -1..+1
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetSentimentScore mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return 0;
        }
    }

    // ── IFundamentalsProvider ──────────────────────────────────────────────────

    public async Task<CompanyProfile?> GetProfile(string symbol)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/profile2?symbol={symbol}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            // Lege response als ticker ontbreekt
            if (!r.TryGetProperty("ticker", out _)) return null;

            return new CompanyProfile(
                Symbol:      r.TryGetProperty("ticker",                out var tick) ? tick.GetString() ?? symbol : symbol,
                Name:        r.TryGetProperty("name",                  out var nm)   ? nm.GetString()   ?? ""     : "",
                Sector:      r.TryGetProperty("gsector",               out var sec)  ? sec.GetString()  ?? ""     : "",
                Industry:    r.TryGetProperty("finnhubIndustry",       out var ind)  ? ind.GetString()  ?? ""     : "",
                Country:     r.TryGetProperty("country",               out var cnt)  ? cnt.GetString()  ?? ""     : "",
                MarketCap:   r.TryGetProperty("marketCapitalization",  out var mc)   ? mc.GetDouble()             : 0,
                Logo:        r.TryGetProperty("logo",                  out var logo) ? logo.GetString() ?? ""     : "",
                WebUrl:      r.TryGetProperty("weburl",                out var web)  ? web.GetString()  ?? ""     : "",
                Description: ""
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetProfile mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<AnalystRating?> GetAnalystRatings(string symbol)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/recommendation?symbol={symbol}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;

            if (!arr.EnumerateArray().Any()) return null;
            var latest = arr.EnumerateArray().First();

            return new AnalystRating(
                Symbol:          symbol,
                Buy:             latest.TryGetProperty("buy",        out var b)  ? b.GetInt32()  : 0,
                Hold:            latest.TryGetProperty("hold",       out var h)  ? h.GetInt32()  : 0,
                Sell:            latest.TryGetProperty("sell",       out var s)  ? s.GetInt32()  : 0,
                StrongBuy:       latest.TryGetProperty("strongBuy",  out var sb) ? sb.GetInt32() : 0,
                StrongSell:      latest.TryGetProperty("strongSell", out var ss) ? ss.GetInt32() : 0,
                TargetPriceMean: 0
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetAnalystRatings mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    // Deel 2: nieuwe methodes

    public async Task<FinancialMetrics?> GetFinancialMetrics(string symbol)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/metric?symbol={symbol}&metric=all&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("metric", out var m)) return null;

            return new FinancialMetrics(
                Symbol:            symbol,
                PeRatio:           GetNullableDouble(m, "peBasicExclExtraTTM"),
                ForwardPe:         GetNullableDouble(m, "peExclExtraAnnual"),
                PbRatio:           GetNullableDouble(m, "pbQuarterly"),
                PsRatio:           GetNullableDouble(m, "psTTM"),
                EvToEbitda:        null,
                ProfitMargin:      GetNullableDouble(m, "netProfitMarginTTM"),
                OperatingMargin:   GetNullableDouble(m, "operatingMarginTTM"),
                ReturnOnEquity:    GetNullableDouble(m, "roeTTM"),
                ReturnOnAssets:    GetNullableDouble(m, "roaTTM"),
                RevenueGrowthYoy:  GetNullableDouble(m, "revenueGrowthQuarterlyYoy"),
                EarningsGrowthYoy: GetNullableDouble(m, "epsGrowthQuarterlyYoy"),
                DebtToEquity:      GetNullableDouble(m, "totalDebt/totalEquityQuarterly"),
                CurrentRatio:      GetNullableDouble(m, "currentRatioQuarterly"),
                QuickRatio:        GetNullableDouble(m, "quickRatioQuarterly"),
                DividendYield:     GetNullableDouble(m, "dividendYieldIndicatedAnnual"),
                PayoutRatio:       GetNullableDouble(m, "payoutRatioTTM"),
                MarketCap:         GetNullableDouble(m, "marketCapitalization"),
                Revenue:           GetNullableDouble(m, "revenueTTM"),
                NetIncome:         GetNullableDouble(m, "netIncomeTTM"),
                SharesOutstanding: null,
                FetchedAt:         DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetFinancialMetrics mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/insider-transactions?symbol={symbol}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data)) return Array.Empty<InsiderTransaction>();

            var cutoff = DateTime.UtcNow.AddMonths(-months);

            return data.EnumerateArray()
                .Select(t =>
                {
                    var dateStr = t.TryGetProperty("transactionDate", out var d) ? d.GetString() : null;
                    if (!DateTime.TryParse(dateStr, out var date)) return null;
                    if (date < cutoff) return null;

                    var shares = t.TryGetProperty("share", out var sh) ? sh.GetInt64() : 0;
                    var price  = t.TryGetProperty("transactionPrice", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetDouble() : 0;

                    return new InsiderTransaction(
                        Symbol:          symbol,
                        Name:            t.TryGetProperty("name",            out var n)  ? n.GetString()  ?? "" : "",
                        Relation:        t.TryGetProperty("filingRelation",  out var r)  ? r.GetString()  ?? "" : "",
                        TransactionType: t.TryGetProperty("transactionType", out var tt) ? tt.GetString() ?? "" : "",
                        Date:            date,
                        Shares:          Math.Abs(shares),
                        PricePerShare:   price,
                        TotalValue:      Math.Abs(shares) * price
                    );
                })
                .Where(t => t != null)
                .Cast<InsiderTransaction>()
                .OrderByDescending(t => t.Date)
                .Take(50)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetInsiderTransactions mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return Array.Empty<InsiderTransaction>();
        }
    }

    public async Task<PriceTarget?> GetPriceTarget(string symbol)
    {
        await RateLimit();
        var url = $"{BaseUrl}/stock/price-target?symbol={symbol}&token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            if (!r.TryGetProperty("targetHigh", out _)) return null;

            return new PriceTarget(
                Symbol:           symbol,
                TargetHigh:       r.TryGetProperty("targetHigh",   out var h)  ? h.GetDouble()  : 0,
                TargetLow:        r.TryGetProperty("targetLow",    out var l)  ? l.GetDouble()  : 0,
                TargetMean:       r.TryGetProperty("targetMean",   out var m)  ? m.GetDouble()  : 0,
                TargetMedian:     r.TryGetProperty("targetMedian", out var md) ? md.GetDouble() : 0,
                NumberOfAnalysts: 0,
                FetchedAt:        DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetPriceTarget mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    private static double? GetNullableDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Null) return null;
        try { return prop.GetDouble(); } catch { return null; }
    }
}
