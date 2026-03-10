// ============================================================
// SwingEdge Screener — C# .NET Worker Service
// Scant EU + US aandelen en stuurt push alerts bij signalen
//
// Stack:
//   - .NET 8 Worker Service (background processing)
//   - Finnhub API (EU + US coverage, gratis tier voor start)
//   - SignalR of Telegram Bot voor push notificaties
//   - Optioneel: EODHD als Europese data beter moet
//
// Setup:
//   dotnet new worker -n SwingEdgeScreener
//   dotnet add package Finnhub.Client
//   dotnet add package Microsoft.Extensions.Http
//   dotnet add package Telegram.Bot
// ============================================================

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace SwingEdgeScreener;

public class ScreenerConfig
{
    public string FinnhubApiKey { get; set; } = "";
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";
    public int ScanIntervalMinutes { get; set; } = 15;
    public int CandleHistoryCount { get; set; } = 100;
    public string Timeframe { get; set; } = "D";
    public double BullThreshold { get; set; } = 0.35;
    public double BearThreshold { get; set; } = -0.35;
    public List<string> Watchlist { get; set; } = new();
}

public record Candle(DateTime Time, double Open, double High, double Low, double Close, long Volume);

public record ScreenerSignal(
    string Symbol, string Exchange, string Direction, double Score, double Price,
    string TrendStatus, string MomentumStatus, string VolatilityStatus, string VolumeStatus, DateTime Timestamp);

public record IndicatorResult(
    double TrendScore, double MomentumScore, double VolatilityScore, double VolumeScore, double NormScore,
    string TrendDesc, string MomDesc, string VolDesc, string VolumDesc, bool SqueezeDetected);

public static class IndicatorEngine
{
    public static double[] Ema(double[] values, int period)
    {
        var ema = new double[values.Length];
        double multiplier = 2.0 / (period + 1);
        ema[0] = values[0];
        for (int i = 1; i < values.Length; i++)
            ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
        return ema;
    }

    public static double[] Sma(double[] values, int period)
    {
        var sma = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (i < period - 1) { sma[i] = values[i]; continue; }
            sma[i] = values.Skip(i - period + 1).Take(period).Average();
        }
        return sma;
    }

    public static double[] Rsi(double[] closes, int period = 14)
    {
        var rsi = new double[closes.Length];
        var gains = new double[closes.Length];
        var losses = new double[closes.Length];
        for (int i = 1; i < closes.Length; i++)
        {
            double change = closes[i] - closes[i - 1];
            gains[i] = change > 0 ? change : 0;
            losses[i] = change < 0 ? -change : 0;
        }
        double avgGain = gains.Skip(1).Take(period).Average();
        double avgLoss = losses.Skip(1).Take(period).Average();
        for (int i = period; i < closes.Length; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
            rsi[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));
        }
        return rsi;
    }

    public static (double[] macd, double[] signal, double[] hist) Macd(
        double[] closes, int fast = 12, int slow = 26, int signalPeriod = 9)
    {
        var emaFast = Ema(closes, fast);
        var emaSlow = Ema(closes, slow);
        var macd = emaFast.Zip(emaSlow, (f, s) => f - s).ToArray();
        var signal = Ema(macd, signalPeriod);
        var hist = macd.Zip(signal, (m, s) => m - s).ToArray();
        return (macd, signal, hist);
    }

    public static double[] Atr(Candle[] candles, int period = 14)
    {
        var atr = new double[candles.Length];
        for (int i = 1; i < candles.Length; i++)
        {
            double tr = Math.Max(candles[i].High - candles[i].Low,
                        Math.Max(Math.Abs(candles[i].High - candles[i - 1].Close),
                                 Math.Abs(candles[i].Low - candles[i - 1].Close)));
            atr[i] = i < period ? tr : (atr[i - 1] * (period - 1) + tr) / period;
        }
        return atr;
    }

    public static (double[] mid, double[] upper, double[] lower) BollingerBands(
        double[] closes, int period = 20, double mult = 2.0)
    {
        var mid = Sma(closes, period);
        var upper = new double[closes.Length];
        var lower = new double[closes.Length];
        for (int i = period - 1; i < closes.Length; i++)
        {
            double std = Math.Sqrt(closes.Skip(i - period + 1).Take(period)
                .Select(c => Math.Pow(c - mid[i], 2)).Average());
            upper[i] = mid[i] + mult * std;
            lower[i] = mid[i] - mult * std;
        }
        return (mid, upper, lower);
    }

    public static IndicatorResult Analyze(Candle[] candles)
    {
        if (candles.Length < 50)
            return new IndicatorResult(0, 0, 0, 0, 0, "Onvoldoende data", "", "", "", false);
        var closes  = candles.Select(c => c.Close).ToArray();
        var volumes = candles.Select(c => (double)c.Volume).ToArray();
        int last    = closes.Length - 1;
        var ema21  = Ema(closes, 21);
        var ema50  = Ema(closes, 50);
        var ema200 = closes.Length >= 200 ? Ema(closes, 200) : Ema(closes, closes.Length);
        bool trendBull = closes[last] > ema21[last] && ema21[last] > ema50[last] && ema50[last] > ema200[last];
        bool trendBear = closes[last] < ema21[last] && ema21[last] < ema50[last];
        double trendScore = trendBull ? 1.0 : trendBear ? -1.0 : 0.0;
        string trendDesc = trendBull ? "Bullish" : trendBear ? "Bearish" : "Neutraal";
        var rsi = Rsi(closes, 14);
        var (macdLine, signalLine, macdHist) = Macd(closes);
        double rsiVal = rsi[last];
        bool rsiOsBounce = rsiVal < 35;
        bool rsiBull = rsiVal < 65 && rsiVal > 50;
        bool rsiBear = rsiVal > 35 && rsiVal < 50;
        bool macdBull = macdLine[last] > signalLine[last] && macdHist[last] > 0;
        bool macdBear = macdLine[last] < signalLine[last] && macdHist[last] < 0;
        bool momBull = (rsiBull || rsiOsBounce) && macdBull;
        bool momBear = rsiBear && macdBear;
        double momScore = momBull ? 1.0 : momBear ? -1.0 : 0.0;
        string momDesc = momBull ? $"Bull (RSI:{rsiVal:F1})" : momBear ? $"Bear (RSI:{rsiVal:F1})" : $"Neutraal (RSI:{rsiVal:F1})";
        var (bbMid, bbUpper, bbLower) = BollingerBands(closes, 20, 2.0);
        var atr = Atr(candles, 14);
        double kcUpper = ema50[last] + 1.5 * atr[last];
        double kcLower = ema50[last] - 1.5 * atr[last];
        bool squeezeOn = bbUpper[last] < kcUpper && bbLower[last] > kcLower;
        double bbWidth = closes.Length > 1 ? (bbUpper[last] - bbLower[last]) / bbMid[last] : 0;
        double bbWidthPrev = closes.Length > 2 ? (bbUpper[last - 1] - bbLower[last - 1]) / bbMid[last - 1] : bbWidth;
        bool volExpanding = bbWidth > bbWidthPrev && !squeezeOn;
        double volDir = volExpanding ? (closes[last] > bbMid[last] ? 1.0 : -1.0) : 0.0;
        string volDesc = squeezeOn ? "Squeeze (laag IV)" : volExpanding ? "Expanding" : "Normaal";
        var volMa = Sma(volumes, 20);
        double volRatio = volMa[last] > 0 ? volumes[last] / volMa[last] : 1.0;
        bool volSpike = volRatio >= 2.0;
        bool volSpikeUp = volSpike && closes[last] > candles[last].Open;
        bool volSpikeDn = volSpike && closes[last] < candles[last].Open;
        double volumeScore = volSpikeUp ? 1.0 : volSpikeDn ? -1.0 : 0.0;
        string volumeDesc = volSpike ? $"Spike x{volRatio:F1}" : $"Normaal x{volRatio:F1}";
        double rawScore = trendScore * 3 + momScore * 2 + volDir * 1 + volumeScore * 2;
        double normScore = rawScore / 8.0;
        return new IndicatorResult(trendScore, momScore, volDir, volumeScore, normScore,
            trendDesc, momDesc, volDesc, volumeDesc, squeezeOn);
    }
}

public class FinnhubClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<FinnhubClient> _logger;
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastCall = DateTime.MinValue;
    private const int MinMsBetweenCalls = 1100;

    public FinnhubClient(HttpClient http, string apiKey, ILogger<FinnhubClient> logger)
    { _http = http; _apiKey = apiKey; _logger = logger; }

    private async Task RateLimit()
    {
        await _rateLimiter.WaitAsync();
        try { var elapsed = (DateTime.UtcNow - _lastCall).TotalMilliseconds;
            if (elapsed < MinMsBetweenCalls) await Task.Delay((int)(MinMsBetweenCalls - elapsed));
            _lastCall = DateTime.UtcNow;
        } finally { _rateLimiter.Release(); }
    }

    public async Task<Candle[]?> GetCandles(string symbol, string resolution, int count)
    {
        await RateLimit();
        long to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long from = resolution == "D" ? to - (count + 30) * 86400L : to - (long)count * 3600 * 4;
        var url = $"https://finnhub.io/api/v1/stock/candle?symbol={symbol}&resolution={resolution}&from={from}&to={to}&token={_apiKey}";
        try {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("s", out var status) || status.GetString() != "ok") return null;
            var times  = root.GetProperty("t").EnumerateArray().Select(x => DateTimeOffset.FromUnixTimeSeconds(x.GetInt64()).DateTime).ToArray();
            var opens  = root.GetProperty("o").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var highs  = root.GetProperty("h").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var lows   = root.GetProperty("l").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var closes = root.GetProperty("c").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var vols   = root.GetProperty("v").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            return times.Select((t, i) => new Candle(t, opens[i], highs[i], lows[i], closes[i], vols[i])).ToArray();
        } catch (Exception ex) { _logger.LogWarning("Candle fetch mislukt voor {Symbol}: {Message}", symbol, ex.Message); return null; }
    }

    public async Task<string[]> GetSymbols(string exchange)
    {
        await RateLimit();
        var url = $"https://finnhub.io/api/v1/stock/symbol?exchange={exchange}&token={_apiKey}";
        try {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().Select(s => s.GetProperty("symbol").GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        } catch (Exception ex) { _logger.LogWarning("Symbol fetch mislukt voor {Exchange}: {Message}", exchange, ex.Message); return Array.Empty<string>(); }
    }
}

public class NotificationService
{
    private readonly HttpClient _http;
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(HttpClient http, string botToken, string chatId, ILogger<NotificationService> logger)
    { _http = http; _botToken = botToken; _chatId = chatId; _logger = logger; }

    public async Task SendSignal(ScreenerSignal signal)
    {
        var emoji = signal.Direction == "BUY" ? "\ud83d\udfe2" : signal.Direction == "SELL" ? "\ud83d\udd34" : "\ud83d\udfe0";
        var msg = $"{emoji} *SwingEdge {signal.Direction}* \u2014 `{signal.Symbol}`\n\ud83d\udcb0 Prijs: {signal.Price:F2}\n\ud83d\udcca Score: {signal.Score * 100:+0.0;-0.0}%\n{signal.TrendStatus}\n{signal.MomentumStatus}\n{signal.VolatilityStatus}\n{signal.VolumeStatus}\n\ud83d\udd50 {signal.Timestamp:dd-MM-yyyy HH:mm} UTC";
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
        var payload = new { chat_id = _chatId, text = msg, parse_mode = "Markdown" };
        try { await _http.PostAsJsonAsync(url, payload); _logger.LogInformation("Notificatie verstuurd: {Direction} {Symbol}", signal.Direction, signal.Symbol); }
        catch (Exception ex) { _logger.LogError("Notificatie mislukt: {Message}", ex.Message); }
    }
}

public class SignalHistory
{
    private readonly Dictionary<string, (string Direction, DateTime Time)> _last = new();
    private const int CooldownMinutes = 60;
    public bool ShouldAlert(string symbol, string direction)
    {
        if (_last.TryGetValue(symbol, out var prev))
            if (prev.Direction == direction && (DateTime.UtcNow - prev.Time).TotalMinutes < CooldownMinutes) return false;
        _last[symbol] = (direction, DateTime.UtcNow);
        return true;
    }
}

public class ScreenerWorker : BackgroundService
{
    private readonly ILogger<ScreenerWorker> _logger;
    private readonly ScreenerConfig _config;
    private readonly FinnhubClient _finnhub;
    private readonly NotificationService _notify;
    private readonly SignalHistory _history = new();
    private readonly (string Exchange, string Label)[] _exchanges = new[]
    {
        ("XAMS", "AEX"), ("XETR", "XETRA"), ("XPAR", "Parijs"), ("US", "US"),
    };

    public ScreenerWorker(ILogger<ScreenerWorker> logger, IOptions<ScreenerConfig> config, FinnhubClient finnhub, NotificationService notify)
    { _logger = logger; _config = config.Value; _finnhub = finnhub; _notify = notify; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SwingEdge Screener gestart");
        while (!stoppingToken.IsCancellationRequested)
        {
            if (IsMarketHours()) { _logger.LogInformation("Scan gestart om {Time}", DateTime.UtcNow); await RunScanCycle(stoppingToken); _logger.LogInformation("Scan klaar"); }
            else { _logger.LogInformation("Buiten markturen, wacht..."); }
            await Task.Delay(TimeSpan.FromMinutes(_config.ScanIntervalMinutes), stoppingToken);
        }
    }

    private async Task RunScanCycle(CancellationToken ct)
    {
        foreach (var (exchange, label) in _exchanges)
        {
            if (ct.IsCancellationRequested) break;
            string[] symbols = _config.Watchlist.Any() ? _config.Watchlist.ToArray() : (await _finnhub.GetSymbols(exchange)).Take(100).ToArray();
            _logger.LogInformation("{Label}: {Count} symbolen scannen", label, symbols.Length);
            foreach (var symbol in symbols)
            {
                if (ct.IsCancellationRequested) break;
                var candles = await _finnhub.GetCandles(symbol, _config.Timeframe, _config.CandleHistoryCount);
                if (candles == null || candles.Length < 50) continue;
                var result = IndicatorEngine.Analyze(candles);
                var lastClose = candles[^1].Close;
                string? direction = null;
                if (result.NormScore >= _config.BullThreshold) direction = "BUY";
                else if (result.NormScore <= _config.BearThreshold) direction = "SELL";
                else if (result.SqueezeDetected) direction = "SQUEEZE";
                if (direction == null) continue;
                if (!_history.ShouldAlert(symbol, direction)) continue;
                var signal = new ScreenerSignal(symbol, label, direction, result.NormScore, lastClose,
                    result.TrendDesc, result.MomDesc, result.VolDesc, result.VolumDesc, DateTime.UtcNow);
                _logger.LogInformation("Signaal: {Direction} {Symbol} @ {Price:F2}", direction, symbol, lastClose);
                await _notify.SendSignal(signal);
            }
        }
    }

    private static bool IsMarketHours()
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(21);
    }
}
