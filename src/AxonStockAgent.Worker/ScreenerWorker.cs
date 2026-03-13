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
    private readonly CandleCacheService _candleCache;

    public ScreenerWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScreenerWorker> logger,
        IOptions<ScreenerConfig> config,
        CandleCacheService candleCache)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config.Value;
        _candleCache = candleCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AxonStockAgent Worker gestart");

        // Wacht even tot de database klaar is bij cold start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        DateOnly? lastEodScanDate = null;
        DateOnly? lastFundamentalsRefresh = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {

            using var modeScope = _scopeFactory.CreateScope();
            var algoSettings = modeScope.ServiceProvider.GetRequiredService<AlgoSettingsService>();
            var realtimeMode = await algoSettings.GetBoolAsync("scan", "realtime_mode", false);

            if (realtimeMode)
            {
                // ── Realtime mode: check trigger, dan scan elke N minuten tijdens markturen ──
                await CheckAndRunTriggerAsync(stoppingToken);

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
                // Check ook op handmatige trigger elke minuut
                await CheckAndRunTriggerAsync(stoppingToken);

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

                        // Weekelijkse fundamentals refresh (zondagnacht)
                        if (now.DayOfWeek == DayOfWeek.Sunday && lastFundamentalsRefresh != today)
                        {
                            try
                            {
                                using var fundScope = _scopeFactory.CreateScope();
                                var fundService = fundScope.ServiceProvider.GetRequiredService<FundamentalsService>();
                                _logger.LogInformation("Wekelijkse fundamentals refresh gestart");
                                var (total, success, failed) = await fundService.RefreshAllMarketSymbolsFundamentals();
                                _logger.LogInformation("Fundamentals refresh: {Success}/{Total} succesvol, {Failed} mislukt", success, total, failed);
                                lastFundamentalsRefresh = today;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Wekelijkse fundamentals refresh mislukt");
                            }
                        }
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

                // Check elke minuut (voor trigger polling en EOD timing)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            } // end try
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Onverwachte fout in worker main loop, wacht 30s voor retry");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
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

    /// <summary>
    /// Controleert of er een pending scan trigger in de DB staat.
    /// Als ja: markeer als running, voer scan uit, markeer als completed/failed.
    /// Geeft true terug als er een trigger verwerkt is.
    /// </summary>
    private async Task<bool> CheckAndRunTriggerAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var trigger = await db.ScanTriggers
            .Where(t => t.Status == "pending")
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (trigger == null) return false;

        trigger.Status    = "running";
        trigger.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Handmatige scan trigger gevonden (id={Id}, door={By}), scan gestart",
            trigger.Id, trigger.RequestedBy);

        try
        {
            var (processed, signals) = await RunScanCycleAsync(ct);

            // Haal trigger opnieuw op vanuit dezelfde scope (staat nu als 'running' opgeslagen)
            using var scope2 = _scopeFactory.CreateScope();
            var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
            var t2 = await db2.ScanTriggers.FindAsync(new object[] { trigger.Id }, ct);
            if (t2 != null)
            {
                t2.Status         = "completed";
                t2.CompletedAt    = DateTime.UtcNow;
                t2.ProcessedCount = processed;
                t2.SignalsCount   = signals;
                await db2.SaveChangesAsync(ct);
            }

            _logger.LogInformation("Handmatige scan trigger (id={Id}) voltooid: {P} symbolen, {S} signalen",
                trigger.Id, processed, signals);
            return true;
        }
        catch (Exception ex)
        {
            using var scope3 = _scopeFactory.CreateScope();
            var db3 = scope3.ServiceProvider.GetRequiredService<AppDbContext>();
            var t3 = await db3.ScanTriggers.FindAsync(new object[] { trigger.Id }, ct);
            if (t3 != null)
            {
                t3.Status       = "failed";
                t3.CompletedAt  = DateTime.UtcNow;
                t3.ErrorMessage = ex.Message;
                await db3.SaveChangesAsync(ct);
            }

            _logger.LogError(ex, "Handmatige scan trigger (id={Id}) mislukt", trigger.Id);
            return false;
        }
    }

    private async Task<(int processed, int signals)> RunScanCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var providers = scope.ServiceProvider.GetRequiredService<ProviderManager>();
        var algoSettings = scope.ServiceProvider.GetRequiredService<AlgoSettingsService>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var fundamentalsService = scope.ServiceProvider.GetRequiredService<FundamentalsService>();

        // Stel candle cache modus in op basis van scan-modus
        var realtimeMode = await algoSettings.GetBoolAsync("scan", "realtime_mode", false);
        _candleCache.SetMode(realtimeMode);
        var cacheStatus = _candleCache.GetStatus();
        _logger.LogInformation("Candle cache: mode={Mode}, TTL={Ttl}",
            cacheStatus.RealtimeMode ? "realtime" : "EOD", cacheStatus.Ttl);

        // ── 1. Haal actieve symbolen op ──
        var scanSource = await algoSettings.GetStringAsync("scan", "scan_source", "market_symbols");

        List<string> symbols;
        if (scanSource == "watchlist")
        {
            symbols = await db.Watchlist
                .Where(w => w.IsActive)
                .Select(w => w.Symbol)
                .ToListAsync(ct);
            _logger.LogInformation("Scan bron: Watchlist ({Count} symbolen)", symbols.Count);
        }
        else
        {
            symbols = await db.MarketSymbols
                .Where(m => m.IsActive)
                .Select(m => m.Symbol)
                .ToListAsync(ct);
            _logger.LogInformation("Scan bron: MarketSymbols ({Count} symbolen)", symbols.Count);

            if (symbols.Count == 0)
            {
                symbols = await db.Watchlist
                    .Where(w => w.IsActive)
                    .Select(w => w.Symbol)
                    .ToListAsync(ct);
                _logger.LogInformation("MarketSymbols leeg, fallback naar Watchlist ({Count} symbolen)", symbols.Count);
            }
        }

        if (symbols.Count == 0)
        {
            _logger.LogInformation("Geen symbolen gevonden om te scannen, skip cycle");
            return (0, 0);
        }

        _logger.LogInformation("Scan cycle gestart: {Count} symbolen uit {Source}", symbols.Count, scanSource);

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

        // Haal scan-gedrag instellingen op
        var dedupWindowMinutes = (int)await algoSettings.GetDecimalAsync("scan", "signal_dedup_minutes", 60m);
        var normalizeMissingSources = await algoSettings.GetBoolAsync("scan", "normalize_missing_sources", true);
        var squeezeMinBars = (int)await algoSettings.GetDecimalAsync("thresholds", "squeeze_min_bars", 3m);
        var volatilityRiskEnabled = await algoSettings.GetBoolAsync("scan", "volatility_risk_enabled", true);

        // ── 3. Init services ──
        var marketProvider = await providers.GetMarketDataProvider();
        var newsProviders = await providers.GetAllNewsProviders();

        if (marketProvider == null)
        {
            _logger.LogWarning("Geen actieve market data provider, skip scan");
            return (0, 0);
        }

        var claudeKeyProvider = scope.ServiceProvider.GetRequiredService<ClaudeApiKeyProvider>();
        var claudeApiKey = await claudeKeyProvider.GetApiKeyAsync() ?? "";

        var claudeService = new ClaudeAnalysisService(
            httpFactory.CreateClient("claude"),
            claudeApiKey,
            loggerFactory.CreateLogger<ClaudeAnalysisService>(),
            _scopeFactory);

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
                    symbol, marketProvider, newsProviders, claudeService, fundamentalsService,
                    techWeight, mlWeight, sentimentWeight, claudeWeight, fundamentalWeight,
                    buyThreshold, sellThreshold, squeezeThreshold,
                    lookbackDays, minVolume, normalizeMissingSources,
                    squeezeMinBars, volatilityRiskEnabled, ct);

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

        // ── 5. Nieuws ophalen na scan ──
        try
        {
            var newsService = scope.ServiceProvider.GetRequiredService<NewsService>();
            _logger.LogInformation("Nieuws ophalen gestart...");
            await newsService.FetchLatestNews();
            await newsService.CalculateSectorSentiment();
            _logger.LogInformation("Nieuws ophalen en sector sentiment berekening voltooid");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nieuws ophalen mislukt (scan resultaten zijn wel opgeslagen)");
        }

        return (processed, signalsGenerated);
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
        FundamentalsService fundamentalsService,
        double techWeight, double mlWeight, double sentimentWeight,
        double claudeWeight, double fundamentalWeight,
        double buyThreshold, double sellThreshold, double squeezeThreshold,
        int lookbackDays, long minVolume, bool normalizeMissingSources,
        int squeezeMinBars, bool volatilityRiskEnabled,
        CancellationToken ct)
    {
        // ── Fetch candles (via cache) ──
        var candles = await _candleCache.GetCandlesAsync(marketProvider, symbol, _config.Timeframe, lookbackDays);
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
        var techNorm   = (techScore + 1) / 2;
        var sentNorm   = (sentimentScore + 1) / 2;
        var claudeNorm = claude?.Confidence ?? 0.5;
        if (claude?.Direction == "SELL") claudeNorm = 1 - claudeNorm;
        var mlNorm   = mlProbability.HasValue ? (double)mlProbability.Value : 0.5;

        // ── Fundamentele analyse ──
        var fundNorm = 0.5;
        try
        {
            var fundamentals = await fundamentalsService.GetFundamentals(symbol);
            if (fundamentals != null)
            {
                var fundResult = FundamentalsScorer.Score(
                    peRatio:           fundamentals.PeRatio,
                    forwardPe:         fundamentals.ForwardPe,
                    pbRatio:           fundamentals.PbRatio,
                    profitMargin:      fundamentals.ProfitMargin,
                    operatingMargin:   fundamentals.OperatingMargin,
                    returnOnEquity:    fundamentals.ReturnOnEquity,
                    revenueGrowthYoy:  fundamentals.RevenueGrowthYoy,
                    earningsGrowthYoy: fundamentals.EarningsGrowthYoy,
                    debtToEquity:      fundamentals.DebtToEquity,
                    currentRatio:      fundamentals.CurrentRatio,
                    analystBuy:        fundamentals.AnalystBuy,
                    analystHold:       fundamentals.AnalystHold,
                    analystSell:       fundamentals.AnalystSell,
                    analystStrongBuy:  fundamentals.AnalystStrongBuy,
                    analystStrongSell: fundamentals.AnalystStrongSell,
                    targetPriceMean:   fundamentals.TargetPriceMean,
                    currentPrice:      candles[^1].Close);

                fundNorm = fundResult.Score;
                _logger.LogDebug("{Symbol} fundamentals: {Score:F2} ({Desc}) [{Details}]",
                    symbol, fundNorm, fundResult.Description, string.Join(", ", fundResult.Details));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fundamentals scoring mislukt voor {Symbol}, gebruik neutraal", symbol);
        }

        var sentPresent   = sentimentScore != 0;
        var claudePresent = claude != null;
        var mlPresent     = mlProbability.HasValue;
        var fundPresent   = fundNorm != 0.5;

        double finalScore;
        if (normalizeMissingSources)
        {
            // Normalize mode: ontbrekende bronnen → 0.5 (neutraal), gewichten tellen altijd mee.
            var sentNormEff   = sentPresent   ? sentNorm   : 0.5;
            var claudeNormEff = claudePresent ? claudeNorm : 0.5;
            var mlNormEff     = mlPresent     ? mlNorm     : 0.5;

            finalScore = techNorm      * techWeight
                       + mlNormEff     * mlWeight
                       + sentNormEff   * sentimentWeight
                       + claudeNormEff * claudeWeight
                       + fundNorm      * fundamentalWeight;

            // Volatility risk multiplier: hoge volatiliteit verlaagt de eindscore
            if (volatilityRiskEnabled)
                finalScore *= indicators.VolatilityRiskMultiplier;
        }
        else
        {
            // Legacy mode: alleen aanwezige bronnen tellen mee
            double totalWeight = techWeight;
            double weightedSum = techNorm * techWeight;

            if (mlPresent)     { totalWeight += mlWeight;        weightedSum += mlNorm   * mlWeight; }
            if (sentPresent)   { totalWeight += sentimentWeight; weightedSum += sentNorm * sentimentWeight; }
            if (claudePresent) { totalWeight += claudeWeight;    weightedSum += claudeNorm * claudeWeight; }
            totalWeight += fundamentalWeight; weightedSum += fundNorm * fundamentalWeight;

            finalScore = totalWeight > 0 ? weightedSum / totalWeight : 0.5;

            if (volatilityRiskEnabled)
                finalScore *= indicators.VolatilityRiskMultiplier;
        }

        var rawScore = volatilityRiskEnabled && indicators.VolatilityRiskMultiplier > 0
            ? finalScore / indicators.VolatilityRiskMultiplier
            : finalScore;

        _logger.LogDebug(
            "{Symbol} score breakdown: tech={Tech:F3}, sent={Sent:F3}({SentP}), claude={Claude:F3}({ClaudeP}), ml={Ml:F3}({MlP}), fund={Fund:F3}({FundP}) → raw={Raw:F3}, volRisk={VolRisk:F2}, final={Final:F3} [{Mode}] [BBpct={BBPct:F2}, sqzBars={SqzBars}]",
            symbol,
            techNorm,
            sentPresent   ? sentNorm   : 0.5, sentPresent   ? "aanwezig" : "neutraal",
            claudePresent ? claudeNorm : 0.5, claudePresent ? "aanwezig" : "neutraal",
            mlPresent     ? mlNorm     : 0.5, mlPresent     ? "aanwezig" : "neutraal",
            fundNorm, fundPresent ? "aanwezig" : "neutraal",
            rawScore,
            indicators.VolatilityRiskMultiplier,
            finalScore,
            normalizeMissingSources ? "normalize" : "legacy",
            indicators.BbWidthPercentile,
            indicators.SqueezeBarCount);

        // ── Verdict bepalen ──
        string verdict;
        string direction;

        if (indicators.SqueezeDetected && indicators.SqueezeBarCount >= squeezeMinBars && finalScore >= squeezeThreshold)
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
            Summary: summary,
            FundamentalsScore: fundPresent ? fundNorm : null,
            NewsScore: sentPresent ? sentNorm : null
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
            existing.VolumeStatus      = signal.BaseSignal.VolumeStatus;
            existing.FundamentalsScore = signal.FundamentalsScore;
            existing.NewsScore         = signal.NewsScore;
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
            FundamentalsScore = signal.FundamentalsScore,
            NewsScore         = signal.NewsScore,
            Notified          = false,
            CreatedAt         = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return true; // is een nieuw signaal
    }

}
