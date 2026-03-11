using System.Globalization;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;
using AxonStockAgent.Core.Analysis;
using AxonStockAgent.Core.Interfaces;
using AxonStockAgent.Core.Models;
using AxonStockAgent.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AxonStockAgent.Worker;

/// <summary>
/// Background worker die periodiek de watchlist scant:
/// 1. Haal actieve symbolen uit de watchlist
/// 2. Per symbool: fetch candles → technische analyse → sentiment → Claude AI
/// 3. Bereken gewogen eindscore op basis van AlgoSettings
/// 4. Sla signalen op in de database (upsert: vervangt bestaand signaal per symbool/verdict)
/// 5. Stuur Telegram notificaties voor relevante signalen
/// </summary>
public class ScreenerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScreenerWorker> _logger;
    private readonly ScreenerConfig _config;

    public ScreenerWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScreenerWorker> logger,
        IOptions<ScreenerConfig> config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AxonStockAgent Worker gestart");

        // Wacht even tot de database klaar is bij cold start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        DateOnly? lastEodScanDate = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var modeScope = _scopeFactory.CreateScope();
            var algoSettings = modeScope.ServiceProvider.GetRequiredService<AlgoSettingsService>();
            var realtimeMode = await algoSettings.GetBoolAsync("scan", "realtime_mode", false);

            if (realtimeMode)
            {
                // ── Realtime mode: scan elke N minuten tijdens markturen ──
                if (IsMarketHours())
                {
                    try
                    {
                        _logger.LogInformation("Realtime scan gestart ({Time} UTC)", DateTime.UtcNow.ToString("HH:mm"));
                        await RunScanCycleAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Realtime scan cycle mislukt");
                    }
                }
                else
                {
                    _logger.LogDebug("Realtime mode: buiten markturen ({Time} UTC)", DateTime.UtcNow.ToString("HH:mm"));
                }

                var intervalMinutes = (int)await algoSettings.GetDecimalAsync("scan", "realtime_interval_minutes", 30m);
                await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, intervalMinutes)), stoppingToken);
            }
            else
            {
                // ── EOD mode: één scan per dag om 22:30 UTC (na US market close) ──
                var now          = DateTime.UtcNow;
                var today        = DateOnly.FromDateTime(now);
                var isAfterClose = now.TimeOfDay >= TimeSpan.FromHours(22.5);
                var isWeekday    = now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
                var notYetRan    = lastEodScanDate != today;

                if (isAfterClose && isWeekday && notYetRan)
                {
                    try
                    {
                        _logger.LogInformation("EOD scan gestart ({Time} UTC)", now.ToString("HH:mm"));
                        await RunScanCycleAsync(stoppingToken);
                        lastEodScanDate = today;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "EOD scan cycle mislukt");
                    }
                }
                else
                {
                    _logger.LogDebug("EOD mode: wacht op 22:30 UTC — nu {Time} UTC", now.ToString("HH:mm"));
                }

                // Check elke 5 minuten of het tijd is voor EOD scan
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private static bool IsMarketHours()
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        // EU open 08:00, US close 21:00 UTC
        return now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(21);
    }

    private async Task RunScanCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var providers = scope.ServiceProvider.GetRequiredService<ProviderManager>();
        var algoSettings = scope.ServiceProvider.GetRequiredService<AlgoSettingsService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // ── 1. Haal actieve symbolen op ──
        var symbols = await db.Watchlist
            .Where(w => w.IsActive)
            .Select(w => w.Symbol)
            .ToListAsync(ct);

        if (symbols.Count == 0)
        {
            _logger.LogInformation("Geen actieve symbolen in watchlist, skip scan");
            return;
        }

        _logger.LogInformation("Scan cycle gestart: {Count} symbolen", symbols.Count);

        // ── 2. Haal gewichten en thresholds op uit AlgoSettings ──
        var techWeight = (double)await algoSettings.GetDecimalAsync("weights", "technical_weight", 0.30m);
        var mlWeight = (double)await algoSettings.GetDecimalAsync("weights", "ml_weight", 0.25m);
        var sentimentWeight = (double)await algoSettings.GetDecimalAsync("weights", "sentiment_weight", 0.20m);
        var claudeWeight = (double)await algoSettings.GetDecimalAsync("weights", "claude_weight", 0.15m);
        var fundamentalWeight = (double)await algoSettings.GetDecimalAsync("weights", "fundamental_weight", 0.10m);

        var buyThreshold = (double)await algoSettings.GetDecimalAsync("thresholds", "buy_threshold", 0.65m);
        var sellThreshold = (double)await algoSettings.GetDecimalAsync("thresholds", "sell_threshold", 0.35m);
        var squeezeThreshold = (double)await algoSettings.GetDecimalAsync("thresholds", "squeeze_threshold", 0.80m);
        var lookbackDays = (int)await algoSettings.GetDecimalAsync("scan", "lookback_days", 90m);
        var minVolume = (long)await algoSettings.GetDecimalAsync("scan", "min_volume", 100000m);

        var notifyBuy = await algoSettings.GetBoolAsync("notifications", "notify_buy", true);
        var notifySell = await algoSettings.GetBoolAsync("notifications", "notify_sell", true);
        var notifySqueeze = await algoSettings.GetBoolAsync("notifications", "notify_squeeze", true);

        // Haal het dedup-window op (standaard 60 minuten)
        var dedupWindowMinutes = (int)await algoSettings.GetDecimalAsync("scan", "signal_dedup_minutes", 60m);

        // ── 3. Init services ──
        var marketProvider = await providers.GetMarketDataProvider();
        var newsProviders = await providers.GetAllNewsProviders();

        if (marketProvider == null)
        {
            _logger.LogWarning("Geen actieve market data provider, skip scan");
            return;
        }

        var claudeService = new ClaudeAnalysisService(
            httpFactory.CreateClient("claude"),
            _config.ClaudeApiKey,
            loggerFactory.CreateLogger<ClaudeAnalysisService>());

        var telegramService = new TelegramNotificationService(
            httpFactory.CreateClient("telegram"),
            _config.TelegramBotToken,
            _config.TelegramChatId,
            loggerFactory.CreateLogger<TelegramNotificationService>());

        int processed = 0, signalsGenerated = 0;

        // ── 4. Scan elk symbool ──
        foreach (var symbol in symbols)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var signal = await ScanSymbolAsync(
                    symbol, marketProvider, newsProviders, claudeService,
                    techWeight, mlWeight, sentimentWeight, claudeWeight, fundamentalWeight,
                    buyThreshold, sellThreshold, squeezeThreshold,
                    lookbackDays, minVolume, ct);

                if (signal != null)
                {
                    var isNew = await UpsertSignalAsync(db, signal, dedupWindowMinutes, ct);
                    signalsGenerated++;

                    bool shouldNotify = signal.FinalVerdict switch
                    {
                        "BUY" => notifyBuy,
                        "SELL" => notifySell,
                        "SQUEEZE" => notifySqueeze,
                        _ => false
                    };

                    // Alleen notificatie sturen bij nieuwe signalen, niet bij updates
                    if (isNew && shouldNotify && telegramService.IsConfigured)
                    {
                        await telegramService.SendSignalAsync(signal, ct);
                    }
                }

                processed++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scan mislukt voor {Symbol}", symbol);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        _logger.LogInformation(
            "Scan cycle voltooid: {Processed}/{Total} symbolen verwerkt, {Signals} signalen gegenereerd",
            processed, symbols.Count, signalsGenerated);
    }

    /// <summary>
    /// Scan één symbool: fetch data → analyse → scoring → verdict
    /// Retourneert null als er onvoldoende data is of geen signaal.
    /// </summary>
    private async Task<AiEnrichedSignal?> ScanSymbolAsync(
        string symbol,
        IMarketDataProvider marketProvider,
        INewsProvider[] newsProviders,
        ClaudeAnalysisService claudeService,
        double techWeight, double mlWeight, double sentimentWeight,
        double claudeWeight, double fundamentalWeight,
        double buyThreshold, double sellThreshold, double squeezeThreshold,
        int lookbackDays, long minVolume,
        CancellationToken ct)
    {
        // ── Fetch candles ──
        var candles = await marketProvider.GetCandles(symbol, _config.Timeframe, lookbackDays);
        if (candles == null || candles.Length < 50)
        {
            _logger.LogDebug("Onvoldoende candles voor {Symbol}: {Count}", symbol, candles?.Length ?? 0);
            return null;
        }

        // Volume check
        var avgVolume = candles.TakeLast(20).Average(c => c.Volume);
        if (avgVolume < minVolume)
        {
            _logger.LogDebug("{Symbol} volume te laag: {Vol:N0} < {Min:N0}", symbol, avgVolume, minVolume);
            return null;
        }

        // ── Technische analyse ──
        var indicators = IndicatorEngine.Analyze(candles);
        var techScore = indicators.NormScore;

        // ── Sentiment ──
        double sentimentScore = 0;
        string[] headlines = Array.Empty<string>();
        try
        {
            var newsProvider = newsProviders.FirstOrDefault();
            if (newsProvider != null)
            {
                sentimentScore = await newsProvider.GetSentimentScore(symbol, 7);
                var articles = await newsProvider.GetNews(symbol, 5);
                headlines = articles.Select(a => a.Headline).ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sentiment ophalen mislukt voor {Symbol}", symbol);
        }

        // ── Claude AI analyse ──
        ClaudeAssessment? claude = null;
        if (_config.EnableClaudeAnalysis)
        {
            claude = await claudeService.AnalyzeAsync(symbol, indicators, sentimentScore, headlines, ct);
        }

        // ── ML probability (placeholder voor toekomstige ML-integratie) ──
        float? mlProbability = null;

        // ── Gewogen eindscore berekenen ──
        var techNorm = (techScore + 1) / 2;
        var sentNorm = (sentimentScore + 1) / 2;
        var claudeNorm = claude?.Confidence ?? 0.5;
        if (claude?.Direction == "SELL") claudeNorm = 1 - claudeNorm;
        var fundNorm = 0.5;

        double totalWeight = techWeight;
        double weightedSum = techNorm * techWeight;

        if (mlProbability.HasValue)  { totalWeight += mlWeight;          weightedSum += (mlProbability.Value) * mlWeight; }
        if (sentimentScore != 0)     { totalWeight += sentimentWeight;   weightedSum += sentNorm * sentimentWeight; }
        if (claude != null)          { totalWeight += claudeWeight;      weightedSum += claudeNorm * claudeWeight; }
        totalWeight += fundamentalWeight; weightedSum += fundNorm * fundamentalWeight;

        var finalScore = totalWeight > 0 ? weightedSum / totalWeight : 0.5;

        // ── Verdict bepalen ──
        string verdict;
        string direction;

        if (indicators.SqueezeDetected && finalScore >= squeezeThreshold)
        {
            verdict = "SQUEEZE";
            direction = "LONG";
        }
        else if (finalScore >= buyThreshold)
        {
            verdict = "BUY";
            direction = "LONG";
        }
        else if (finalScore <= sellThreshold)
        {
            verdict = "SELL";
            direction = "SHORT";
        }
        else
        {
            _logger.LogDebug("{Symbol}: score {Score:F2} → HOLD (geen signaal)", symbol, finalScore);
            return null;
        }

        var currentPrice = candles[^1].Close;

        var baseSignal = new ScreenerSignal(
            Symbol: symbol,
            Exchange: "",
            Direction: direction,
            Score: techScore,
            Price: currentPrice,
            TrendStatus: indicators.TrendDesc,
            MomentumStatus: indicators.MomDesc,
            VolatilityStatus: indicators.VolDesc,
            VolumeStatus: indicators.VolumDesc,
            Timestamp: DateTime.UtcNow
        );

        var summary = $"{symbol}: {verdict} @ €{currentPrice:F2} " +
                       $"(tech={techScore:F2}, sent={sentimentScore:F2}" +
                       $"{(claude != null ? $", claude={claude.Direction}/{claude.Confidence:F2}" : "")})";

        _logger.LogInformation("📊 Signaal: {Summary}", summary);

        return new AiEnrichedSignal(
            BaseSignal: baseSignal,
            MlProbability: mlProbability,
            SentimentScore: sentimentScore,
            Claude: claude,
            FinalScore: finalScore,
            FinalVerdict: verdict,
            Summary: summary
        );
    }

    /// <summary>
    /// Upsert een signaal: als er al een signaal bestaat voor hetzelfde symbool
    /// en verdict binnen het dedup-window, update dat bestaande signaal.
    /// Anders maak een nieuw signaal aan.
    /// Retourneert true als het een nieuw signaal is, false bij update.
    /// </summary>
    private async Task<bool> UpsertSignalAsync(AppDbContext db, AiEnrichedSignal signal, int dedupWindowMinutes, CancellationToken ct)
    {
        var windowStart = DateTime.UtcNow.AddMinutes(-dedupWindowMinutes);

        // Zoek bestaand signaal voor hetzelfde symbool + verdict binnen het window
        var existing = await db.Signals
            .Where(s => s.Symbol == signal.BaseSignal.Symbol
                        && s.FinalVerdict == signal.FinalVerdict
                        && s.CreatedAt >= windowStart)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            // ── Update bestaand signaal ──
            existing.Direction = signal.BaseSignal.Direction;
            existing.TechScore = signal.BaseSignal.Score;
            existing.MlProbability = signal.MlProbability;
            existing.SentimentScore = signal.SentimentScore;
            existing.ClaudeConfidence = signal.Claude?.Confidence;
            existing.ClaudeDirection = signal.Claude?.Direction;
            existing.ClaudeReasoning = signal.Claude?.Reasoning;
            existing.FinalScore = signal.FinalScore;
            existing.PriceAtSignal = signal.BaseSignal.Price;
            existing.TrendStatus = signal.BaseSignal.TrendStatus;
            existing.MomentumStatus = signal.BaseSignal.MomentumStatus;
            existing.VolatilityStatus = signal.BaseSignal.VolatilityStatus;
            existing.VolumeStatus = signal.BaseSignal.VolumeStatus;
            // CreatedAt bewust NIET updaten — behoud originele timestamp

            _logger.LogDebug("📝 Signaal geüpdatet: {Symbol} {Verdict} (id={Id})",
                signal.BaseSignal.Symbol, signal.FinalVerdict, existing.Id);

            await db.SaveChangesAsync(ct);
            return false; // is een update, geen nieuw signaal
        }

        // ── Nieuw signaal aanmaken ──
        db.Signals.Add(new SignalEntity
        {
            Symbol = signal.BaseSignal.Symbol,
            Direction = signal.BaseSignal.Direction,
            TechScore = signal.BaseSignal.Score,
            MlProbability = signal.MlProbability,
            SentimentScore = signal.SentimentScore,
            ClaudeConfidence = signal.Claude?.Confidence,
            ClaudeDirection = signal.Claude?.Direction,
            ClaudeReasoning = signal.Claude?.Reasoning,
            FinalScore = signal.FinalScore,
            FinalVerdict = signal.FinalVerdict,
            PriceAtSignal = signal.BaseSignal.Price,
            TrendStatus = signal.BaseSignal.TrendStatus,
            MomentumStatus = signal.BaseSignal.MomentumStatus,
            VolatilityStatus = signal.BaseSignal.VolatilityStatus,
            VolumeStatus = signal.BaseSignal.VolumeStatus,
            Notified = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return true; // is een nieuw signaal
    }

}
