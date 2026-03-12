using System.Text.Json;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Api.Providers;

/// <summary>
/// EODHD implementatie van IMarketDataProvider, INewsProvider en IFundamentalsProvider.
/// Betaald plan ($19.99/mnd+). Uitstekende EU dekking.
/// Rate limit: ~100 calls/min voor standaard plan.
/// Docs: https://eodhd.com/financial-apis/
/// </summary>
public class EodhdProvider : IMarketDataProvider, INewsProvider, IFundamentalsProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<EodhdProvider> _logger;

    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastCall = DateTime.MinValue;
    private const int MinMsBetweenCalls = 650; // ~90/min, marge houden
    private const string BaseUrl = "https://eodhd.com/api";

    public string Name => "eodhd";
    public bool SupportsRealtime => false; // EODHD is EOD + delayed
    public string[] SupportedExchanges => ["US", "AS", "XETRA", "PA", "LSE", "MI", "SW", "TO", "HK", "SHG", "SHE"];

    public EodhdProvider(HttpClient http, string apiKey, ILogger<EodhdProvider> logger)
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

    /// <summary>
    /// Converteer intern symbool (bijv. "ASML.AS") naar EODHD formaat.
    /// EODHD gebruikt {TICKER}.{EXCHANGE_CODE}, bijv. "ASML.AS" of "AAPL.US".
    /// Als het symbool al een punt bevat, nemen we aan dat het al EODHD-formaat is.
    /// Anders voegen we ".US" toe als default.
    /// </summary>
    private static string ToEodhdSymbol(string symbol)
    {
        // Al in EODHD formaat (bevat een punt)
        if (symbol.Contains('.')) return symbol;
        // Default naar US exchange
        return $"{symbol}.US";
    }

    private static double? SafeDouble(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Null || val.ValueKind == JsonValueKind.String) return null;
        try { return val.GetDouble(); } catch { return null; }
    }

    private static string SafeString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return "";
        return val.GetString() ?? "";
    }

    private static int SafeInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.Null) return 0;
        try { return val.GetInt32(); } catch { return 0; }
    }

    private static long? SafeLong(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Null) return null;
        try { return val.GetInt64(); } catch { return null; }
    }

    // ── IMarketDataProvider ──────────────────────────────────────────────────

    public async Task<Candle[]?> GetCandles(string symbol, string resolution, int count)
    {
        await RateLimit();
        var eodhSymbol = ToEodhdSymbol(symbol);
        var period = resolution switch { "D" => "d", "W" => "w", "M" => "m", _ => "d" };
        var from = DateTime.UtcNow.AddDays(-(count + 30) * (period == "w" ? 7 : 1)).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var url = $"{BaseUrl}/eod/{eodhSymbol}?from={from}&to={to}&period={period}&api_token={_apiKey}&fmt=json";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            return doc.RootElement.EnumerateArray()
                .Select(c =>
                {
                    var dateStr = SafeString(c, "date");
                    DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt);
                    return new Candle(
                        dt,
                        SafeDouble(c, "open") ?? 0,
                        SafeDouble(c, "high") ?? 0,
                        SafeDouble(c, "low") ?? 0,
                        SafeDouble(c, "adjusted_close") ?? SafeDouble(c, "close") ?? 0, // gebruik adjusted
                        (long)(SafeDouble(c, "volume") ?? 0)
                    );
                })
                .TakeLast(count)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetCandles mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<string[]> GetSymbols(string exchange)
    {
        await RateLimit();
        var url = $"{BaseUrl}/exchange-symbol-list/{exchange}?api_token={_apiKey}&fmt=json";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(s => SafeString(s, "Code"))
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => $"{s}.{exchange}") // EODHD formaat
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetSymbols mislukt voor {Exchange}: {Message}", exchange, ex.Message);
            return Array.Empty<string>();
        }
    }

    public async Task<Quote?> GetQuote(string symbol)
    {
        await RateLimit();
        var eodSymbol = ToEodhdSymbol(symbol);
        var url = $"{BaseUrl}/real-time/{eodSymbol}?api_token={_apiKey}&fmt=json";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            var c = r.TryGetProperty("close", out var cp) ? cp.GetDouble() : 0;
            if (c == 0) return null;

            var pc = r.TryGetProperty("previousClose", out var pcp) ? pcp.GetDouble() : 0;
            var change = c - pc;

            return new Quote
            {
                Symbol        = symbol,
                CurrentPrice  = c,
                PreviousClose = pc,
                Change        = change,
                ChangePercent = pc > 0 ? change / pc * 100 : 0,
                High          = r.TryGetProperty("high",   out var h) ? h.GetDouble() : 0,
                Low           = r.TryGetProperty("low",    out var l) ? l.GetDouble() : 0,
                Open          = r.TryGetProperty("open",   out var o) ? o.GetDouble() : 0,
                Volume        = r.TryGetProperty("volume", out var v) ? v.GetInt64()  : 0,
                Timestamp     = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetQuote mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    // ── INewsProvider ────────────────────────────────────────────────────────

    public async Task<NewsArticle[]> GetNews(string? symbol = null, int limit = 20)
    {
        await RateLimit();
        var url = symbol != null
            ? $"{BaseUrl}/news?s={ToEodhdSymbol(symbol)}&limit={limit}&api_token={_apiKey}&fmt=json"
            : $"{BaseUrl}/news?limit={limit}&api_token={_apiKey}&fmt=json";

        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.EnumerateArray()
                .Take(limit)
                .Select(a =>
                {
                    var dateStr = SafeString(a, "date");
                    DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var publishedAt);

                    // EODHD biedt ingebouwd sentiment
                    double sentimentScore = 0;
                    if (a.TryGetProperty("sentiment", out var sent))
                    {
                        sentimentScore = SafeDouble(sent, "polarity") ?? 0;
                    }

                    return new NewsArticle(
                        Headline:       SafeString(a, "title"),
                        Summary:        SafeString(a, "content").Length > 500
                                            ? SafeString(a, "content")[..500] + "..."
                                            : SafeString(a, "content"),
                        Url:            SafeString(a, "link"),
                        Symbol:         symbol ?? "",
                        SentimentScore: sentimentScore,
                        PublishedAt:    publishedAt != default ? publishedAt : DateTime.UtcNow,
                        Source:         "eodhd"
                    );
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetNews mislukt: {Message}", ex.Message);
            return Array.Empty<NewsArticle>();
        }
    }

    public async Task<double> GetSentimentScore(string symbol, int days = 7)
    {
        // Haal recente nieuws op en bereken gemiddeld sentiment
        var news = await GetNews(symbol, limit: 30);
        if (news.Length == 0) return 0;

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var recent = news.Where(n => n.PublishedAt >= cutoff).ToArray();
        if (recent.Length == 0) return 0;

        return recent.Average(n => n.SentimentScore);
    }

    // ── IFundamentalsProvider ────────────────────────────────────────────────

    public async Task<CompanyProfile?> GetProfile(string symbol)
    {
        await RateLimit();
        var eodhSymbol = ToEodhdSymbol(symbol);
        var url = $"{BaseUrl}/fundamentals/{eodhSymbol}?api_token={_apiKey}&fmt=json&filter=General,Highlights";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("General", out var gen)) return null;

            double marketCap = 0;
            if (root.TryGetProperty("Highlights", out var hi))
                marketCap = SafeDouble(hi, "MarketCapitalization") ?? 0;

            var logoUrl = SafeString(gen, "LogoURL");
            if (!string.IsNullOrEmpty(logoUrl) && !logoUrl.StartsWith("http"))
                logoUrl = $"https://eodhistoricaldata.com{logoUrl}";

            return new CompanyProfile(
                Symbol:      symbol,
                Name:        SafeString(gen, "Name"),
                Sector:      SafeString(gen, "Sector"),
                Industry:    SafeString(gen, "Industry"),
                Country:     SafeString(gen, "CountryISO"),
                MarketCap:   marketCap / 1_000_000, // EODHD geeft absolute waarde, wij slaan op in miljoenen
                Logo:        logoUrl,
                WebUrl:      SafeString(gen, "WebURL"),
                Description: SafeString(gen, "Description")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetProfile mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<AnalystRating?> GetAnalystRatings(string symbol)
    {
        await RateLimit();
        var eodhSymbol = ToEodhdSymbol(symbol);
        var url = $"{BaseUrl}/fundamentals/{eodhSymbol}?api_token={_apiKey}&fmt=json&filter=AnalystRatings";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("AnalystRatings", out var ar)) return null;

            return new AnalystRating(
                Symbol:          symbol,
                StrongBuy:       SafeInt(ar, "StrongBuy"),
                Buy:             SafeInt(ar, "Buy"),
                Hold:            SafeInt(ar, "Hold"),
                Sell:            SafeInt(ar, "Sell"),
                StrongSell:      SafeInt(ar, "StrongSell"),
                TargetPriceMean: SafeDouble(ar, "TargetPrice") ?? 0
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetAnalystRatings mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<FinancialMetrics?> GetFinancialMetrics(string symbol)
    {
        await RateLimit();
        var eodhSymbol = ToEodhdSymbol(symbol);
        var url = $"{BaseUrl}/fundamentals/{eodhSymbol}?api_token={_apiKey}&fmt=json&filter=Highlights,Valuation,SharesStats";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement hi = default, val = default, ss = default;
            root.TryGetProperty("Highlights", out hi);
            root.TryGetProperty("Valuation", out val);
            root.TryGetProperty("SharesStats", out ss);

            if (hi.ValueKind == JsonValueKind.Undefined && val.ValueKind == JsonValueKind.Undefined)
                return null;

            return new FinancialMetrics(
                Symbol:             symbol,
                PeRatio:            SafeDouble(val, "TrailingPE") ?? SafeDouble(hi, "PERatio"),
                ForwardPe:          SafeDouble(val, "ForwardPE"),
                PbRatio:            SafeDouble(val, "PriceBookMRQ"),
                PsRatio:            SafeDouble(val, "PriceSalesTTM"),
                EvToEbitda:         SafeDouble(val, "EnterpriseValueEbitda"),
                ProfitMargin:       SafeDouble(hi, "ProfitMargin"),
                OperatingMargin:    SafeDouble(hi, "OperatingMarginTTM"),
                ReturnOnEquity:     SafeDouble(hi, "ReturnOnEquityTTM"),
                ReturnOnAssets:     SafeDouble(hi, "ReturnOnAssetsTTM"),
                RevenueGrowthYoy:   SafeDouble(hi, "QuarterlyRevenueGrowthYOY"),
                EarningsGrowthYoy:  SafeDouble(hi, "QuarterlyEarningsGrowthYOY"),
                DebtToEquity:       null, // niet direct beschikbaar in deze filter
                CurrentRatio:       null, // zou apart opgehaald kunnen worden
                QuickRatio:         null,
                DividendYield:      SafeDouble(hi, "DividendYield"),
                PayoutRatio:        SafeDouble(hi, "PayoutRatio"),
                MarketCap:          SafeDouble(hi, "MarketCapitalization"),
                Revenue:            SafeDouble(hi, "RevenueTTM"),
                NetIncome:          null, // niet direct in Highlights
                SharesOutstanding:  SafeLong(ss, "SharesOutstanding"),
                FetchedAt:          DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EODHD GetFinancialMetrics mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public async Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3)
    {
        await RateLimit();
        var eodhSymbol = ToEodhdSymbol(symbol);
        var from = DateTime.UtcNow.AddMonths(-months).ToString("yyyy-MM-dd");
        var url = $"{BaseUrl}/insider-transactions?code={eodhSymbol}&from={from}&api_token={_apiKey}&fmt=json";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<InsiderTransaction>();

            return doc.RootElement.EnumerateArray()
                .Select(t =>
                {
                    var dateStr = SafeString(t, "date");
                    if (!DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var date)) return null;

                    var shares = Math.Abs(SafeDouble(t, "transactionShares") ?? 0);
                    var price  = SafeDouble(t, "transactionPrice") ?? 0;

                    return new InsiderTransaction(
                        Symbol:          symbol,
                        Name:            SafeString(t, "ownerName"),
                        Relation:        SafeString(t, "ownerTitle"),
                        TransactionType: SafeString(t, "transactionType"),
                        Date:            date,
                        Shares:          (long)shares,
                        PricePerShare:   price,
                        TotalValue:      shares * price
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
            _logger.LogWarning("EODHD GetInsiderTransactions mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return Array.Empty<InsiderTransaction>();
        }
    }

    public async Task<PriceTarget?> GetPriceTarget(string symbol)
    {
        // EODHD combineert price target in AnalystRatings
        var ratings = await GetAnalystRatings(symbol);
        if (ratings == null || ratings.TargetPriceMean <= 0) return null;

        return new PriceTarget(
            Symbol:           symbol,
            TargetHigh:       ratings.TargetPriceMean * 1.2, // Schatting: EODHD geeft alleen gemiddelde
            TargetLow:        ratings.TargetPriceMean * 0.8,
            TargetMean:       ratings.TargetPriceMean,
            TargetMedian:     ratings.TargetPriceMean, // Geen aparte mediaan beschikbaar
            NumberOfAnalysts: ratings.Buy + ratings.Hold + ratings.Sell + ratings.StrongBuy + ratings.StrongSell,
            FetchedAt:        DateTime.UtcNow
        );
    }
}
