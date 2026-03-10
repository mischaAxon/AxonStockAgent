// ============================================================
// SwingEdge Dividend Tracker
//
// Features:
//   - Aankomende ex-dividend datums (alert 3 dagen van tevoren)
//   - Dividend yield per aandeel in watchlist
//   - Historische dividend groei (5 jaar trend + CAGR)
//   - Portfolio dividend inkomsten (jaarlijkse cashflow)
//
// Data bronnen:
//   - Finnhub: /stock/dividend2 endpoint (gratis tier)
//   - EODHD: /div/{symbol} (betere EU coverage)
// ============================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace SwingEdgeScreener;

// DATA MODELLEN

public record DividendPayment(DateTime ExDate, DateTime PayDate, double Amount, string Currency, string Symbol);

public record DividendProfile(
    string Symbol, string Exchange, double CurrentYield, double AnnualDividend,
    double FiveYearCagr, DividendGrowthTrend Trend, DividendPayment[] History,
    DividendPayment? NextEx, DateTime LastUpdated);

public enum DividendGrowthTrend { Growing, Stable, Declining, Irregular, NoDividend }

public record PortfolioDividendSummary(
    double TotalAnnualIncome, double AverageYield, DividendPortfolioItem[] Items,
    UpcomingExDate[] UpcomingExDates, double ProjectedMonthlyIncome);

public record DividendPortfolioItem(
    string Symbol, int Shares, double PricePerShare, double AnnualDividendPerShare,
    double Yield, double AnnualIncome, DividendGrowthTrend Trend, DateTime? NextExDate);

public record UpcomingExDate(string Symbol, DateTime ExDate, DateTime? PayDate, double Amount, int DaysUntil);

public record PortfolioPosition(string Symbol, int Shares);

// DIVIDEND DATA CLIENT

public class DividendDataClient
{
    private readonly HttpClient _http;
    private readonly string _finnhubKey;
    private readonly string? _eodhdKey;
    private readonly ILogger&lt;DividendDataClient&gt; _logger;
    private readonly Dictionary&lt;string, (DividendProfile Profile, DateTime CachedAt)&gt; _cache = new();
    private const int CacheHours = 12;

    public DividendDataClient(HttpClient http, string finnhubKey, string? eodhdKey, ILogger&lt;DividendDataClient&gt; logger)
    { _http = http; _finnhubKey = finnhubKey; _eodhdKey = eodhdKey; _logger = logger; }

    public async Task&lt;DividendProfile?&gt; GetDividendProfile(string symbol, double currentPrice)
    {
        if (_cache.TryGetValue(symbol, out var cached) &amp;&amp; (DateTime.UtcNow - cached.CachedAt).TotalHours &lt; CacheHours)
            return cached.Profile;

        DividendPayment[]? payments = null;
        if (_eodhdKey != null &amp;&amp; (symbol.Contains(".AS") || symbol.Contains(".DE") || symbol.Contains(".PA") || symbol.Contains(".L")))
            payments = await FetchFromEodhd(symbol);
        payments ??= await FetchFromFinnhub(symbol);

        if (payments == null || payments.Length == 0)
            return new DividendProfile(symbol, GetExchange(symbol), 0, 0, 0, DividendGrowthTrend.NoDividend, Array.Empty&lt;DividendPayment&gt;(), null, DateTime.UtcNow);

        var profile = BuildProfile(symbol, payments, currentPrice);
        _cache[symbol] = (profile, DateTime.UtcNow);
        return profile;
    }

    private async Task&lt;DividendPayment[]?&gt; FetchFromFinnhub(string symbol)
    {
        try
        {
            var from = DateTime.UtcNow.AddYears(-6).ToString("yyyy-MM-dd");
            var to = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");
            var url = $"https://finnhub.io/api/v1/stock/dividend2?symbol={symbol}&amp;from={from}&amp;to={to}&amp;token={_finnhubKey}";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            return data.EnumerateArray()
                .Select(d =&gt; new DividendPayment(ParseDate(d, "exDate"), ParseDate(d, "payDate"),
                    d.TryGetProperty("amount", out var a) ? a.GetDouble() : 0, d.TryGetProperty("currency", out var c) ? c.GetString() ?? "EUR" : "EUR", symbol))
                .Where(p =&gt; p.Amount &gt; 0).OrderByDescending(p =&gt; p.ExDate).ToArray();
        }
        catch (Exception ex) { _logger.LogWarning("Finnhub dividend mislukt voor {Symbol}: {Message}", symbol, ex.Message); return null; }
    }

    private async Task&lt;DividendPayment[]?&gt; FetchFromEodhd(string symbol)
    {
        try
        {
            var from = DateTime.UtcNow.AddYears(-6).ToString("yyyy-MM-dd");
            var url = $"https://eodhd.com/api/div/{symbol}?api_token={_eodhdKey}&amp;fmt=json&amp;from={from}";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(d =&gt; new DividendPayment(DateTime.Parse(d.GetProperty("date").GetString()!),
                    d.TryGetProperty("paymentDate", out var pd) ? DateTime.Parse(pd.GetString()!) : DateTime.MinValue,
                    d.GetProperty("value").GetDouble(), d.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "EUR" : "EUR", symbol))
                .Where(p =&gt; p.Amount &gt; 0).OrderByDescending(p =&gt; p.ExDate).ToArray();
        }
        catch (Exception ex) { _logger.LogWarning("EODHD dividend mislukt voor {Symbol}: {Message}", symbol, ex.Message); return null; }
    }

    private DividendProfile BuildProfile(string symbol, DividendPayment[] payments, double currentPrice)
    {
        var now = DateTime.UtcNow;
        var lastYear = payments.Where(p =&gt; p.ExDate &gt;= now.AddYears(-1)).ToArray();
        double annualDividend = lastYear.Sum(p =&gt; p.Amount);
        double yield = currentPrice &gt; 0 ? annualDividend / currentPrice * 100 : 0;
        double cagr = Calculate5YearCagr(payments);
        var trend = DetermineTrend(payments);
        var nextEx = payments.Where(p =&gt; p.ExDate &gt; now).OrderBy(p =&gt; p.ExDate).FirstOrDefault();
        var history = payments.Where(p =&gt; p.ExDate &gt;= now.AddYears(-5)).OrderByDescending(p =&gt; p.ExDate).ToArray();
        return new DividendProfile(symbol, GetExchange(symbol), Math.Round(yield, 2), Math.Round(annualDividend, 4),
            Math.Round(cagr, 2), trend, history, nextEx, now);
    }

    private double Calculate5YearCagr(DividendPayment[] payments)
    {
        var now = DateTime.UtcNow;
        var byYear = payments.Where(p =&gt; p.ExDate &gt;= now.AddYears(-6) &amp;&amp; p.ExDate &lt; now)
            .GroupBy(p =&gt; p.ExDate.Year).OrderBy(g =&gt; g.Key)
            .Select(g =&gt; new { g.Key, Total = g.Sum(p =&gt; p.Amount) }).ToList();
        if (byYear.Count &lt; 2) return 0;
        double startDiv = byYear.First().Total; double endDiv = byYear.Last().Total;
        int years = byYear.Last().Key - byYear.First().Key;
        if (startDiv &lt;= 0 || years &lt;= 0) return 0;
        return (Math.Pow(endDiv / startDiv, 1.0 / years) - 1) * 100;
    }

    private DividendGrowthTrend DetermineTrend(DividendPayment[] payments)
    {
        var byYear = payments.Where(p =&gt; p.ExDate &gt;= DateTime.UtcNow.AddYears(-5))
            .GroupBy(p =&gt; p.ExDate.Year).OrderBy(g =&gt; g.Key).Select(g =&gt; g.Sum(p =&gt; p.Amount)).ToList();
        if (byYear.Count &lt; 2) return DividendGrowthTrend.Irregular;
        int increases = 0, decreases = 0;
        for (int i = 1; i &lt; byYear.Count; i++)
        { if (byYear[i] &gt; byYear[i - 1] * 1.01) increases++; else if (byYear[i] &lt; byYear[i - 1] * 0.99) decreases++; }
        int total = byYear.Count - 1;
        if (decreases == 0 &amp;&amp; increases &gt;= total * 0.6) return DividendGrowthTrend.Growing;
        if (increases == 0 &amp;&amp; decreases &gt;= total * 0.6) return DividendGrowthTrend.Declining;
        if (increases == 0 &amp;&amp; decreases == 0) return DividendGrowthTrend.Stable;
        return DividendGrowthTrend.Irregular;
    }

    private static DateTime ParseDate(JsonElement el, string prop) =&gt;
        el.TryGetProperty(prop, out var v) &amp;&amp; v.GetString() is string s &amp;&amp; !string.IsNullOrEmpty(s) ? DateTime.Parse(s) : DateTime.MinValue;

    private static string GetExchange(string symbol) =&gt;
        symbol.Contains(".AS") ? "Euronext AMS" : symbol.Contains(".DE") ? "XETRA" :
        symbol.Contains(".PA") ? "Euronext PAR" : symbol.Contains(".L") ? "LSE" : "US";
}

// PORTFOLIO DIVIDEND SERVICE

public class DividendPortfolioService
{
    private readonly DividendDataClient _client;
    private readonly FinnhubClient _finnhub;
    private readonly ILogger&lt;DividendPortfolioService&gt; _logger;

    public DividendPortfolioService(DividendDataClient client, FinnhubClient finnhub, ILogger&lt;DividendPortfolioService&gt; logger)
    { _client = client; _finnhub = finnhub; _logger = logger; }

    public async Task&lt;PortfolioDividendSummary&gt; GetPortfolioSummary(List&lt;PortfolioPosition&gt; positions)
    {
        var items = new List&lt;DividendPortfolioItem&gt;();
        foreach (var pos in positions)
        {
            var candles = await _finnhub.GetCandles(pos.Symbol, "D", 5);
            double price = candles?.LastOrDefault()?.Close ?? 0;
            var profile = await _client.GetDividendProfile(pos.Symbol, price);
            if (profile == null) continue;
            items.Add(new DividendPortfolioItem(pos.Symbol, pos.Shares, Math.Round(price, 2),
                profile.AnnualDividend, profile.CurrentYield, Math.Round(pos.Shares * profile.AnnualDividend, 2), profile.Trend, profile.NextEx?.ExDate));
        }
        var upcoming = items.Where(i =&gt; i.NextExDate.HasValue)
            .Select(i =&gt; new UpcomingExDate(i.Symbol, i.NextExDate!.Value, null, i.AnnualDividendPerShare / 4, (int)(i.NextExDate.Value - DateTime.UtcNow).TotalDays))
            .Where(u =&gt; u.DaysUntil &gt;= 0 &amp;&amp; u.DaysUntil &lt;= 90).OrderBy(u =&gt; u.DaysUntil).ToArray();
        double totalIncome = items.Sum(i =&gt; i.AnnualIncome);
        double avgYield = items.Where(i =&gt; i.Yield &gt; 0).Select(i =&gt; i.Yield).DefaultIfEmpty(0).Average();
        return new PortfolioDividendSummary(Math.Round(totalIncome, 2), Math.Round(avgYield, 2),
            items.OrderByDescending(i =&gt; i.AnnualIncome).ToArray(), upcoming, Math.Round(totalIncome / 12, 2));
    }
}

// DIVIDEND ALERT SERVICE

public class DividendAlertService
{
    private readonly DividendDataClient _client;
    private readonly FinnhubClient _finnhub;
    private readonly NotificationService _notify;
    private readonly ILogger&lt;DividendAlertService&gt; _logger;
    private readonly HashSet&lt;string&gt; _sentAlerts = new();

    public DividendAlertService(DividendDataClient client, FinnhubClient finnhub, NotificationService notify, ILogger&lt;DividendAlertService&gt; logger)
    { _client = client; _finnhub = finnhub; _notify = notify; _logger = logger; }

    public async Task CheckUpcomingExDates(List&lt;string&gt; watchlist)
    {
        foreach (var symbol in watchlist)
        {
            var candles = await _finnhub.GetCandles(symbol, "D", 5);
            double price = candles?.LastOrDefault()?.Close ?? 0;
            var profile = await _client.GetDividendProfile(symbol, price);
            if (profile?.NextEx == null) continue;
            int daysUntil = (int)(profile.NextEx.ExDate - DateTime.UtcNow.Date).TotalDays;
            if (daysUntil &lt; 0 || daysUntil &gt; 3) continue;
            string alertKey = $"{symbol}-{profile.NextEx.ExDate:yyyy-MM-dd}";
            if (_sentAlerts.Contains(alertKey)) continue;
            var signal = new ScreenerSignal(symbol, profile.Exchange, "DIVIDEND", profile.CurrentYield / 10, price,
                $"Dividend: {profile.NextEx.Currency} {profile.NextEx.Amount:F4}", $"Ex-datum: {profile.NextEx.ExDate:dd-MM-yyyy}",
                $"Trend: {profile.Trend} | CAGR: {profile.FiveYearCagr:+0.0;-0.0}%", $"Yield: {profile.CurrentYield:F2}%", DateTime.UtcNow);
            await _notify.SendSignal(signal);
            _sentAlerts.Add(alertKey);
        }
    }
}

// DIVIDEND REPORT SERVICE

public class DividendReportService
{
    private readonly DividendPortfolioService _portfolio;
    private readonly NotificationService _notify;
    private readonly ILogger&lt;DividendReportService&gt; _logger;

    public DividendReportService(DividendPortfolioService portfolio, NotificationService notify, ILogger&lt;DividendReportService&gt; logger)
    { _portfolio = portfolio; _notify = notify; _logger = logger; }

    public async Task SendWeeklyReport(List&lt;PortfolioPosition&gt; positions)
    {
        if (positions.Count == 0) { _logger.LogInformation("Geen portfolio posities geconfigureerd"); return; }
        var summary = await _portfolio.GetPortfolioSummary(positions);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SwingEdge Dividend Rapport");
        sb.AppendLine($"Totaal jaarinkomen: EUR {summary.TotalAnnualIncome:N2}");
        sb.AppendLine($"Gemiddeld per maand: EUR {summary.ProjectedMonthlyIncome:N2}");
        sb.AppendLine($"Gemiddeld yield: {summary.AverageYield:F2}%");
        foreach (var item in summary.Items.Take(5))
            sb.AppendLine($"  {item.Symbol} - {item.Yield:F2}% yield - EUR {item.AnnualIncome:N2}/jaar ({item.Shares} aandelen)");
        if (summary.UpcomingExDates.Length &gt; 0)
        {
            sb.AppendLine("Aankomende ex-datums:");
            foreach (var ex in summary.UpcomingExDates.Take(5))
                sb.AppendLine($"  {ex.Symbol} - {ex.ExDate:dd-MM} (over {ex.DaysUntil}d)");
        }
        var signal = new ScreenerSignal("PORTFOLIO", "Dividend Rapport", "RAPPORT", summary.AverageYield / 10, 0,
            sb.ToString(), "", "", "", DateTime.UtcNow);
        await _notify.SendSignal(signal);
    }
}
