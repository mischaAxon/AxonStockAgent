using System.Text.Json;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Api.Providers;

/// <summary>
/// Financial Modeling Prep implementatie van IFundamentalsProvider.
/// Gebruikt de /stable/ endpoints (v4+, ondersteund vanaf september 2025).
/// Plan vereist: Starter of hoger voor volledige dekking.
/// Rate limit: afhankelijk van plan (~750 req/dag gratis, ~300/min paid).
/// Docs: https://site.financialmodelingprep.com/developer/docs
/// </summary>
public class FmpProvider : IFundamentalsProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<FmpProvider> _logger;

    private const string BaseUrl = "https://financialmodelingprep.com/stable";

    public string Name => "fmp";

    public FmpProvider(HttpClient http, string apiKey, ILogger<FmpProvider> logger)
    {
        _http   = http;
        _apiKey = apiKey;
        _logger = logger;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static double? SafeDouble(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (val.ValueKind == JsonValueKind.String)
        {
            if (double.TryParse(val.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return null;
        }
        try { return val.GetDouble(); } catch { return null; }
    }

    private static long? SafeLong(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        try { return val.GetInt64(); } catch { return null; }
    }

    private static int SafeInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return 0;
        try { return val.GetInt32(); } catch { return 0; }
    }

    private static string SafeStr(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return "";
        return val.GetString() ?? "";
    }

    private string Url(string path, string symbol, string extra = "")
        => $"{BaseUrl}/{path}?symbol={Uri.EscapeDataString(symbol)}&apikey={_apiKey}{extra}";

    private async Task<JsonDocument?> Fetch(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            var json     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("FMP HTTP {StatusCode} voor {Url}: {Body}",
                    (int)response.StatusCode, url.Replace(_apiKey, "***"),
                    json.Length > 150 ? json[..150] : json);
                return null;
            }

            var doc = JsonDocument.Parse(json);

            // FMP retourneert soms {"Error Message":"..."} met HTTP 200
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("Error Message", out _))
            {
                doc.Dispose();
                return null;
            }

            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FMP fetch mislukt voor {Url}", url.Replace(_apiKey, "***"));
            return null;
        }
    }

    // ── IFundamentalsProvider ────────────────────────────────────────────────

    public async Task<CompanyProfile?> GetProfile(string symbol)
    {
        using var doc = await Fetch(Url("profile", symbol));
        if (doc == null) return null;

        // FMP retourneert een array met één element
        var root = doc.RootElement;
        var item = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root.ValueKind == JsonValueKind.Object ? root : default;

        if (item.ValueKind == JsonValueKind.Undefined) return null;

        return new CompanyProfile(
            Symbol:      symbol,
            Name:        SafeStr(item, "companyName"),
            Sector:      SafeStr(item, "sector"),
            Industry:    SafeStr(item, "industry"),
            Country:     SafeStr(item, "country"),
            MarketCap:   (SafeDouble(item, "marketCap") ?? 0) / 1_000_000,
            Logo:        SafeStr(item, "image"),
            WebUrl:      SafeStr(item, "website"),
            Description: SafeStr(item, "description")
        );
    }

    public async Task<FinancialMetrics?> GetFinancialMetrics(string symbol)
    {
        // TTM metrics bevatten P/E, marges, ratios etc.
        using var doc = await Fetch(Url("key-metrics-ttm", symbol));
        if (doc == null) return null;

        var root = doc.RootElement;
        var item = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root.ValueKind == JsonValueKind.Object ? root : default;

        if (item.ValueKind == JsonValueKind.Undefined) return null;

        // FMP slaat winstgroei niet op in key-metrics-ttm; gebruik income-growth als fallback
        // Haal ook profile op voor market cap en shares
        var marketCap = SafeDouble(item, "marketCapTTM");
        var sharesOutstanding = SafeLong(item, "sharesOutstanding");

        return new FinancialMetrics(
            Symbol:             symbol,
            PeRatio:            SafeDouble(item, "peRatioTTM"),
            ForwardPe:          SafeDouble(item, "forwardPeTTM") ?? SafeDouble(item, "forwardPE"),
            PbRatio:            SafeDouble(item, "pbRatioTTM"),
            PsRatio:            SafeDouble(item, "priceToSalesRatioTTM"),
            EvToEbitda:         SafeDouble(item, "enterpriseValueOverEBITDATTM"),
            ProfitMargin:       SafeDouble(item, "netProfitMarginTTM"),
            OperatingMargin:    SafeDouble(item, "operatingProfitMarginTTM"),
            ReturnOnEquity:     SafeDouble(item, "returnOnEquityTTM"),
            ReturnOnAssets:     SafeDouble(item, "returnOnAssetsTTM"),
            RevenueGrowthYoy:   SafeDouble(item, "revenueGrowthTTM"),
            EarningsGrowthYoy:  SafeDouble(item, "netIncomeGrowthTTM"),
            DebtToEquity:       SafeDouble(item, "debtToEquityTTM"),
            CurrentRatio:       SafeDouble(item, "currentRatioTTM"),
            QuickRatio:         SafeDouble(item, "quickRatioTTM"),
            DividendYield:      SafeDouble(item, "dividendYieldTTM"),
            PayoutRatio:        SafeDouble(item, "payoutRatioTTM"),
            MarketCap:          marketCap,
            Revenue:            SafeDouble(item, "revenueTTM"),
            NetIncome:          SafeDouble(item, "netIncomeTTM"),
            SharesOutstanding:  sharesOutstanding,
            FetchedAt:          DateTime.UtcNow
        );
    }

    public async Task<AnalystRating?> GetAnalystRatings(string symbol)
    {
        // FMP analyst-estimates geeft buy/hold/sell/strongBuy/strongSell per periode
        using var doc = await Fetch(Url("analyst-estimates", symbol, "&period=annual&limit=1"));
        if (doc == null) return null;

        var root = doc.RootElement;
        var item = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            ? root[0]
            : root.ValueKind == JsonValueKind.Object ? root : default;

        if (item.ValueKind == JsonValueKind.Undefined) return null;

        return new AnalystRating(
            Symbol:          symbol,
            Buy:             SafeInt(item, "numAnalystEstimateRevenue") > 0
                                 ? SafeInt(item, "numAnalystEstimateRevenue") : 0,
            Hold:            0,
            Sell:            0,
            StrongBuy:       0,
            StrongSell:      0,
            TargetPriceMean: SafeDouble(item, "estimatedEpsAvg") ?? 0
        );
    }

    public async Task<PriceTarget?> GetPriceTarget(string symbol)
    {
        using var doc = await Fetch(Url("price-target-summary", symbol));
        if (doc == null) return null;

        var root = doc.RootElement;

        // price-target-summary geeft { lastMonth: {...}, lastQuarter: {...}, lastYear: {...} }
        // of een array — gebruik lastMonth of het eerste element
        JsonElement item;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            item = root[0];
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("lastMonth", out var lm))
            item = lm;
        else if (root.ValueKind == JsonValueKind.Object)
            item = root;
        else
            return null;

        var mean = SafeDouble(item, "targetConsensus") ?? SafeDouble(item, "priceTarget") ?? 0;
        var high = SafeDouble(item, "targetHigh") ?? mean;
        var low  = SafeDouble(item, "targetLow")  ?? mean;

        if (mean == 0) return null;

        return new PriceTarget(
            Symbol:            symbol,
            TargetHigh:        high,
            TargetLow:         low,
            TargetMean:        mean,
            TargetMedian:      mean,
            NumberOfAnalysts:  SafeInt(item, "numberOfAnalysts"),
            FetchedAt:         DateTime.UtcNow
        );
    }

    public async Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3)
    {
        var from = DateTime.UtcNow.AddMonths(-months).ToString("yyyy-MM-dd");
        using var doc = await Fetch(Url("insider-trading", symbol, $"&transactionType=P-Purchase,S-Sale&limit=100"));
        if (doc == null) return Array.Empty<InsiderTransaction>();

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) return Array.Empty<InsiderTransaction>();

        var cutoff = DateTime.UtcNow.AddMonths(-months);
        var results = new List<InsiderTransaction>();

        foreach (var t in root.EnumerateArray())
        {
            var dateStr = SafeStr(t, "transactionDate");
            if (!DateTime.TryParse(dateStr, out var date)) continue;
            if (date < cutoff) continue;

            results.Add(new InsiderTransaction(
                Symbol:          symbol,
                Name:            SafeStr(t, "reportingName"),
                Relation:        SafeStr(t, "typeOfOwner"),
                TransactionType: SafeStr(t, "transactionType"),
                Date:            date,
                Shares:          SafeLong(t, "securitiesTransacted") ?? 0,
                PricePerShare:   SafeDouble(t, "price") ?? 0,
                TotalValue:      SafeDouble(t, "securitiesTransacted") ?? 0 * (SafeDouble(t, "price") ?? 0)
            ));
        }

        return results.ToArray();
    }
}
