using System.Globalization;
using System.Text.Json;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Api.Providers;

/// <summary>
/// Twelve Data implementatie van IMarketDataProvider.
/// Biedt realtime quotes voor EU en US markten via batch /quote endpoint.
/// Free plan: 8 req/min, 800/dag. Grow ($29/mnd): 30 req/min, geen daglimiet.
/// Docs: https://twelvedata.com/docs
/// </summary>
public class TwelveDataProvider : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<TwelveDataProvider> _logger;

    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastCall = DateTime.MinValue;
    private const int MinMsBetweenCalls = 500; // max 2 calls/sec voor paid plan
    private const string BaseUrl = "https://api.twelvedata.com";

    public string Name => "twelvedata";
    public bool SupportsRealtime => true;
    public string[] SupportedExchanges => ["US", "AS", "XETRA", "PA", "LSE", "MI", "SW", "TO"];

    public TwelveDataProvider(HttpClient http, string apiKey, ILogger<TwelveDataProvider> logger)
    {
        _http   = http;
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
    /// Converteer intern EODHD-formaat symbool ("ASML.AS") naar Twelve Data formaat ("ASML:XAMS").
    /// US-symbolen zonder exchange-suffix worden ongewijzigd doorgegeven.
    /// </summary>
    private static string ToTwelveSymbol(string symbol)
    {
        if (!symbol.Contains('.')) return symbol;

        var dot    = symbol.LastIndexOf('.');
        var ticker = symbol[..dot];
        var suffix = symbol[(dot + 1)..].ToUpperInvariant();

        var mic = suffix switch
        {
            "AS"    => "XAMS", // Amsterdam
            "PA"    => "XPAR", // Parijs
            "XETRA" => "XETR", // Frankfurt Xetra
            "LSE"   => "XLON", // Londen
            "L"     => "XLON",
            "MI"    => "XMIL", // Milaan
            "SW"    => "XSWX", // Zürich
            "TO"    => "XTSE", // Toronto
            "HK"    => "XHKG", // Hong Kong
            "US"    => null,   // US — geen exchange code nodig
            _       => null
        };

        return mic != null ? $"{ticker}:{mic}" : ticker;
    }

    private static double SafeDouble(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.String)
            return double.TryParse(val.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        try { return val.GetDouble(); } catch { return 0; }
    }

    private static string SafeString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return "";
        return val.GetString() ?? "";
    }

    private Quote? ParseQuote(JsonElement el, string originalSymbol)
    {
        // Error response check
        if (el.TryGetProperty("status", out var status) && status.GetString() == "error")
        {
            _logger.LogDebug("TwelveData fout voor {Symbol}: {Msg}", originalSymbol, SafeString(el, "message"));
            return null;
        }

        var close = SafeDouble(el, "close");
        if (close == 0) return null;

        DateTime timestamp;
        if (el.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.Number)
            timestamp = DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64()).UtcDateTime;
        else if (el.TryGetProperty("datetime", out var dt) && DateTime.TryParse(dt.GetString(), out var parsed))
            timestamp = parsed;
        else
            timestamp = DateTime.UtcNow;

        return new Quote
        {
            Symbol        = originalSymbol,
            CurrentPrice  = close,
            PreviousClose = SafeDouble(el, "previous_close"),
            Change        = SafeDouble(el, "change"),
            ChangePercent = SafeDouble(el, "percent_change"),
            High          = SafeDouble(el, "high"),
            Low           = SafeDouble(el, "low"),
            Open          = SafeDouble(el, "open"),
            Volume        = (long)SafeDouble(el, "volume"),
            Timestamp     = timestamp,
        };
    }

    // ── IMarketDataProvider ──────────────────────────────────────────────────

    public async Task<Quote?> GetQuote(string symbol)
    {
        await RateLimit();
        var tdSymbol = ToTwelveSymbol(symbol);
        var url = $"{BaseUrl}/quote?symbol={Uri.EscapeDataString(tdSymbol)}&apikey={_apiKey}";

        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return ParseQuote(doc.RootElement, symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("TwelveData GetQuote mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Haal quotes op voor meerdere symbolen in één API call.
    /// Twelve Data ondersteunt batch via komma-gescheiden symbolen in /quote.
    /// Retourneert dict keyed op origineel symbool (EODHD-formaat).
    /// </summary>
    public async Task<Dictionary<string, Quote>> GetBulkQuotes(string[] symbols)
    {
        var result = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        if (symbols.Length == 0) return result;

        // Twelve Data ondersteunt tot 120 symbolen per batch call
        const int chunkSize = 50;

        for (int i = 0; i < symbols.Length; i += chunkSize)
        {
            var chunk     = symbols.Skip(i).Take(chunkSize).ToArray();
            var tdSymbols = chunk.Select(ToTwelveSymbol).ToArray();
            var param     = string.Join(",", tdSymbols);

            await RateLimit();
            var url = $"{BaseUrl}/quote?symbol={Uri.EscapeDataString(param)}&apikey={_apiKey}";

            try
            {
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object) continue;

                if (chunk.Length == 1)
                {
                    // Enkel symbool → directe quote response
                    var q = ParseQuote(root, chunk[0]);
                    if (q != null) result[chunk[0]] = q;
                }
                else
                {
                    // Meerdere symbolen → dict keyed op "TICKER:MIC"
                    for (int j = 0; j < chunk.Length; j++)
                    {
                        var tdKey = tdSymbols[j];
                        if (root.TryGetProperty(tdKey, out var el))
                        {
                            var q = ParseQuote(el, chunk[j]);
                            if (q != null) result[chunk[j]] = q;
                        }
                    }
                }

                _logger.LogDebug("TwelveData bulk: {Got}/{Total} quotes opgehaald", result.Count, symbols.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("TwelveData GetBulkQuotes chunk {Start}-{End} mislukt: {Message}",
                    i, i + chunk.Length, ex.Message);
            }
        }

        return result;
    }

    public async Task<Candle[]?> GetCandles(string symbol, string resolution, int count)
    {
        await RateLimit();
        var tdSymbol = ToTwelveSymbol(symbol);

        var interval = resolution switch
        {
            "1"  => "1min",  "5"  => "5min",  "15" => "15min",
            "30" => "30min", "60" => "1h",    "D"  => "1day",
            "W"  => "1week", "M"  => "1month",
            _    => "1day"
        };

        var url = $"{BaseUrl}/time_series?symbol={Uri.EscapeDataString(tdSymbol)}&interval={interval}&outputsize={count}&apikey={_apiKey}";

        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("values", out var values)) return null;

            return values.EnumerateArray()
                .Select(c =>
                {
                    DateTime.TryParse(SafeString(c, "datetime"), out var dt);
                    return new Candle(
                        dt,
                        SafeDouble(c, "open"),
                        SafeDouble(c, "high"),
                        SafeDouble(c, "low"),
                        SafeDouble(c, "close"),
                        (long)SafeDouble(c, "volume")
                    );
                })
                .Reverse() // Twelve Data geeft nieuwste eerst
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("TwelveData GetCandles mislukt voor {Symbol}: {Message}", symbol, ex.Message);
            return null;
        }
    }

    public Task<string[]> GetSymbols(string exchange) => Task.FromResult(Array.Empty<string>());
}
