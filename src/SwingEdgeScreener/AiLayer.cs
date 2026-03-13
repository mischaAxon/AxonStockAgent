// ============================================================
// SwingEdge AI Layer — Drie gecombineerde AI-lagen
//
// Laag 1: ML.NET — traint op historische candles, voorspelt
//          kans op positief rendement na signaal (+5% in 10 dagen)
//
// Laag 2: Claude API — beoordeelt signaal + nieuws context,
//          geeft reasoning en confidence score terug
//
// Laag 3: Finnhub Sentiment — nieuwssentiment als derde filter
//
// Eindoordeel: gewogen combinatie van alle drie
//
// NuGet packages nodig:
//   dotnet add package Microsoft.ML
//   dotnet add package Microsoft.ML.FastTree
//   dotnet add package Anthropic.SDK   (of gebruik HttpClient direct)
// ============================================================

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace SwingEdgeScreener;

// LAAG 1: ML.NET — VOORSPELLINGSMODEL

public class SignalFeatures
{
    [LoadColumn(0)] public float TrendScore { get; set; }
    [LoadColumn(1)] public float MomScore { get; set; }
    [LoadColumn(2)] public float VolScore { get; set; }
    [LoadColumn(3)] public float VolumeScore { get; set; }
    [LoadColumn(4)] public float RsiValue { get; set; }
    [LoadColumn(5)] public float MacdHistNorm { get; set; }
    [LoadColumn(6)] public float BbWidthNorm { get; set; }
    [LoadColumn(7)] public float VolumeRatio { get; set; }
    [LoadColumn(8)] public float Ema21Slope { get; set; }
    [LoadColumn(9)] public float PriceVsEma50 { get; set; }
    [LoadColumn(10)] public float DayOfWeek { get; set; }
    [LoadColumn(11)] public bool Label { get; set; }
}

public class SignalPrediction
{
    [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
    [ColumnName("Probability")]    public float Probability { get; set; }
    [ColumnName("Score")]          public float Score { get; set; }
}

public class MlSignalModel
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private readonly string _modelPath;
    private readonly ILogger&lt;MlSignalModel&gt; _logger;

    public bool IsTrained => _model != null;

    public MlSignalModel(ILogger&lt;MlSignalModel&gt; logger, string modelPath = "swingmodel.zip")
    {
        _mlContext = new MLContext(seed: 42);
        _modelPath = modelPath;
        _logger = logger;
        if (File.Exists(modelPath))
        {
            _model = _mlContext.Model.Load(modelPath, out _);
            _logger.LogInformation("ML model geladen van {Path}", modelPath);
        }
    }

    public static SignalFeatures ExtractFeatures(Candle[] candles, IndicatorResult indicators, bool? label = null)
    {
        var closes = candles.Select(c => c.Close).ToArray();
        var volumes = candles.Select(c => (double)c.Volume).ToArray();
        int last = closes.Length - 1;
        var ema21 = IndicatorEngine.Ema(closes, 21);
        var ema50 = IndicatorEngine.Ema(closes, 50);
        float ema21Slope = last >= 5 ? (float)((ema21[last] - ema21[last - 5]) / ema21[last - 5] * 100) : 0f;
        float priceVsEma50 = ema50[last] > 0 ? (float)((closes[last] - ema50[last]) / ema50[last] * 100) : 0f;
        var (_, _, hist) = IndicatorEngine.Macd(closes);
        float macdHistNorm = closes[last] > 0 ? (float)(hist[last] / closes[last] * 100) : 0f;
        var (bbMid, bbUpper, bbLower) = IndicatorEngine.BollingerBands(closes, 20, 2.0);
        float bbWidth = bbMid[last] > 0 ? (float)((bbUpper[last] - bbLower[last]) / bbMid[last]) : 0f;
        var bbWidths = Enumerable.Range(last - 19, 20).Where(i => i >= 0 &amp;&amp; bbMid[i] > 0).Select(i => (bbUpper[i] - bbLower[i]) / bbMid[i]).ToArray();
        float bbWidthNorm = bbWidths.Length > 0 ? bbWidth / (float)bbWidths.Average() : 1f;
        var volMa = IndicatorEngine.Sma(volumes, 20);
        float volRatio = volMa[last] > 0 ? (float)(volumes[last] / volMa[last]) : 1f;
        var rsi = IndicatorEngine.Rsi(closes, 14);
        return new SignalFeatures
        {
            TrendScore = (float)indicators.TrendScore, MomScore = (float)indicators.MomentumScore,
            VolScore = (float)indicators.VolatilityScore, VolumeScore = (float)indicators.VolumeScore,
            RsiValue = (float)rsi[last], MacdHistNorm = macdHistNorm, BbWidthNorm = bbWidthNorm,
            VolumeRatio = volRatio, Ema21Slope = ema21Slope, PriceVsEma50 = priceVsEma50,
            DayOfWeek = (float)candles[last].Time.DayOfWeek, Label = label ?? false
        };
    }

    public void Train(List&lt;(Candle[] Candles, IndicatorResult Indicators, bool WasGoodSignal)&gt; trainingData)
    {
        _logger.LogInformation("ML training gestart met {Count} samples", trainingData.Count);
        var features = trainingData.Select(d => ExtractFeatures(d.Candles, d.Indicators, d.WasGoodSignal)).ToList();
        var dataView = _mlContext.Data.LoadFromEnumerable(features);
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(SignalFeatures.TrendScore), nameof(SignalFeatures.MomScore),
                nameof(SignalFeatures.VolScore), nameof(SignalFeatures.VolumeScore),
                nameof(SignalFeatures.RsiValue), nameof(SignalFeatures.MacdHistNorm),
                nameof(SignalFeatures.BbWidthNorm), nameof(SignalFeatures.VolumeRatio),
                nameof(SignalFeatures.Ema21Slope), nameof(SignalFeatures.PriceVsEma50),
                nameof(SignalFeatures.DayOfWeek))
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: nameof(SignalFeatures.Label),
                numberOfTrees: 100, numberOfLeaves: 20, minimumExampleCountPerLeaf: 10));
        var cvResults = _mlContext.BinaryClassification.CrossValidate(dataView, pipeline, numberOfFolds: 5, labelColumnName: nameof(SignalFeatures.Label));
        double avgAuc = cvResults.Average(r => r.Metrics.AreaUnderRocCurve);
        _logger.LogInformation("ML training klaar — gemiddeld AUC: {Auc:F3}", avgAuc);
        _model = pipeline.Fit(dataView);
        _mlContext.Model.Save(_model, dataView.Schema, _modelPath);
        _logger.LogInformation("Model opgeslagen naar {Path}", _modelPath);
    }

    public SignalPrediction? Predict(Candle[] candles, IndicatorResult indicators)
    {
        if (_model == null) { _logger.LogWarning("ML model niet getraind"); return null; }
        var engine = _mlContext.Model.CreatePredictionEngine&lt;SignalFeatures, SignalPrediction&gt;(_model);
        return engine.Predict(ExtractFeatures(candles, indicators));
    }
}

public static class TrainingDataBuilder
{
    public static List&lt;(Candle[], IndicatorResult, bool)&gt; BuildFromHistory(
        Candle[] allCandles, double targetReturn = 0.05, int holdingDays = 10)
    {
        var result = new List&lt;(Candle[], IndicatorResult, bool)&gt;();
        for (int i = 200; i &lt; allCandles.Length - holdingDays; i++)
        {
            var window = allCandles.Take(i + 1).ToArray();
            var indicators = IndicatorEngine.Analyze(window);
            if (Math.Abs(indicators.NormScore) &lt; 0.35) continue;
            double entryPrice = allCandles[i].Close;
            double exitPrice = allCandles[i + holdingDays].Close;
            bool wasGood = indicators.NormScore > 0
                ? (exitPrice - entryPrice) / entryPrice >= targetReturn
                : (entryPrice - exitPrice) / entryPrice >= targetReturn;
            result.Add((window, indicators, wasGood));
        }
        return result;
    }
}

// LAAG 2: CLAUDE API — REASONING &amp; CONTEXT

public record ClaudeAssessment(double Confidence, string Direction, string Reasoning, bool NewsConfirms, string[] KeyFactors);

public class ClaudeAnalyzer
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger&lt;ClaudeAnalyzer&gt; _logger;

    public ClaudeAnalyzer(HttpClient http, string apiKey, ILogger&lt;ClaudeAnalyzer&gt; logger)
    { _http = http; _apiKey = apiKey; _logger = logger; }

    public async Task&lt;ClaudeAssessment?&gt; Assess(string symbol, ScreenerSignal signal, SignalPrediction? mlPrediction, NewsItem[] recentNews, double sentimentScore)
    {
        var newsText = recentNews.Length > 0 ? string.Join("\n", recentNews.Take(5).Select(n => $"- [{n.Sentiment:+0.00}] {n.Headline}")) : "Geen recent nieuws beschikbaar.";
        var mlText = mlPrediction != null ? $"ML model voorspelling: {(mlPrediction.PredictedLabel ? "positief" : "negatief")} (kans: {mlPrediction.Probability:P1})" : "ML model: niet beschikbaar";
        var prompt = $"Je bent een ervaren swing trader. Beoordeel dit signaal.\n\nAandeel: {symbol}\nRichting: {signal.Direction}\nPrijs: {signal.Price:F2}\nScore: {signal.Score * 100:+0.0;-0.0}%\nTrend: {signal.TrendStatus}\nMomentum: {signal.MomentumStatus}\nVolatiliteit: {signal.VolatilityStatus}\nVolume: {signal.VolumeStatus}\n\n{mlText}\nSentiment: {sentimentScore:+0.00}\n\nNieuws:\n{newsText}\n\nGeef JSON: {{\"confidence\": 0.0-1.0, \"direction\": \"BUY|SELL|NEUTRAL|AVOID\", \"reasoning\": \"max 2 zinnen\", \"news_confirms\": true|false, \"key_factors\": [\"...\"]}}";
        try
        {
            var request = new { model = "claude-sonnet-4-20250514", max_tokens = 400, messages = new[] { new { role = "user", content = prompt } } };
            var requestJson = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = new StringContent(requestJson, Encoding.UTF8, "application/json") };
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            var response = await _http.SendAsync(httpRequest);
            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            var jsonStart = text.IndexOf('{'); var jsonEnd = text.LastIndexOf('}');
            if (jsonStart &lt; 0 || jsonEnd &lt; 0) return null;
            using var parsed = JsonDocument.Parse(text[jsonStart..(jsonEnd + 1)]);
            var root = parsed.RootElement;
            return new ClaudeAssessment(root.GetProperty("confidence").GetDouble(), root.GetProperty("direction").GetString() ?? "NEUTRAL",
                root.GetProperty("reasoning").GetString() ?? "", root.GetProperty("news_confirms").GetBoolean(),
                root.GetProperty("key_factors").EnumerateArray().Select(x => x.GetString() ?? "").ToArray());
        }
        catch (Exception ex) { _logger.LogWarning("Claude analyse mislukt voor {Symbol}: {Message}", symbol, ex.Message); return null; }
    }
}

// LAAG 3: NIEUWS SENTIMENT (Finnhub)

public record NewsItem(string Headline, double Sentiment, DateTime Published, string Source);

public class SentimentAnalyzer
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger&lt;SentimentAnalyzer&gt; _logger;

    public SentimentAnalyzer(HttpClient http, string apiKey, ILogger&lt;SentimentAnalyzer&gt; logger)
    { _http = http; _apiKey = apiKey; _logger = logger; }

    public async Task&lt;(NewsItem[] News, double AvgSentiment)&gt; GetSentiment(string symbol, int days = 7)
    {
        var from = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var url = $"https://finnhub.io/api/v1/company-news?symbol={symbol}&amp;from={from}&amp;to={to}&amp;token={_apiKey}";
        try
        {
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray().Take(20)
                .Select(n => new NewsItem(n.GetProperty("headline").GetString() ?? "", n.TryGetProperty("sentiment", out var s) ? s.GetDouble() : 0.0,
                    DateTimeOffset.FromUnixTimeSeconds(n.GetProperty("datetime").GetInt64()).DateTime, n.GetProperty("source").GetString() ?? "")).ToArray();
            return (items, items.Length > 0 ? items.Average(x => x.Sentiment) : 0.0);
        }
        catch (Exception ex) { _logger.LogWarning("Sentiment fetch mislukt voor {Symbol}: {Message}", symbol, ex.Message); return (Array.Empty&lt;NewsItem&gt;(), 0.0); }
    }
}

// AI ORCHESTRATOR — combineert alle drie lagen

public record AiEnrichedSignal(ScreenerSignal BaseSignal, float? MlProbability, double SentimentScore, ClaudeAssessment? Claude, double FinalScore, string FinalVerdict, string Summary, double? FundamentalsScore = null, double? NewsScore = null);

public class AiOrchestrator
{
    private readonly MlSignalModel _ml;
    private readonly ClaudeAnalyzer _claude;
    private readonly SentimentAnalyzer _sentiment;
    private readonly ILogger&lt;AiOrchestrator&gt; _logger;

    public AiOrchestrator(MlSignalModel ml, ClaudeAnalyzer claude, SentimentAnalyzer sentiment, ILogger&lt;AiOrchestrator&gt; logger)
    { _ml = ml; _claude = claude; _sentiment = sentiment; _logger = logger; }

    public async Task&lt;AiEnrichedSignal&gt; Enrich(string symbol, Candle[] candles, IndicatorResult indicators, ScreenerSignal baseSignal)
    {
        var mlPrediction = _ml.IsTrained ? _ml.Predict(candles, indicators) : null;
        var (news, avgSentiment) = await _sentiment.GetSentiment(symbol);
        var claudeAssessment = await _claude.Assess(symbol, baseSignal, mlPrediction, news, avgSentiment);
        double finalScore = ComputeFinalScore(baseSignal, mlPrediction, avgSentiment, claudeAssessment);
        string verdict = DetermineVerdict(baseSignal.Direction, finalScore, claudeAssessment);
        string summary = BuildSummary(symbol, baseSignal, mlPrediction, avgSentiment, claudeAssessment, finalScore, verdict);
        return new AiEnrichedSignal(baseSignal, mlPrediction?.Probability, avgSentiment, claudeAssessment, finalScore, verdict, summary);
    }

    private double ComputeFinalScore(ScreenerSignal signal, SignalPrediction? ml, double sentiment, ClaudeAssessment? claude)
    {
        double techScore = signal.Score;
        double mlScore = ml != null ? (ml.Probability - 0.5) * 2 : 0;
        double sentScore = Math.Clamp(sentiment, -1, 1);
        double claudeScore = claude != null ? claude.Direction switch { "BUY" => claude.Confidence, "SELL" => -claude.Confidence, "AVOID" => -0.5, _ => 0 } : 0;
        return techScore * 0.35 + mlScore * 0.25 + sentScore * 0.15 + claudeScore * 0.25;
    }

    private string DetermineVerdict(string techDirection, double finalScore, ClaudeAssessment? claude)
    {
        if (claude?.Direction == "AVOID") return "SKIP";
        bool claudeAligns = claude == null || (techDirection == "BUY" &amp;&amp; claude.Direction == "BUY") || (techDirection == "SELL" &amp;&amp; claude.Direction == "SELL") || techDirection == "SQUEEZE";
        if (!claudeAligns) return "SKIP";
        return finalScore switch { >= 0.35 => "BUY", &lt;= -0.35 => "SELL", _ when techDirection == "SQUEEZE" => "SQUEEZE", _ => "SKIP" };
    }

    private string BuildSummary(string symbol, ScreenerSignal signal, SignalPrediction? ml, double sentiment, ClaudeAssessment? claude, double finalScore, string verdict)
    {
        var sb = new StringBuilder();
        string emoji = verdict switch { "BUY" => "G", "SELL" => "R", "SQUEEZE" => "O", _ => "W" };
        sb.AppendLine($"{emoji} SwingEdge AI - {verdict} | {symbol}");
        sb.AppendLine($"Prijs: {signal.Price:F2} | Score: {finalScore * 100:+0.0;-0.0}%");
        sb.AppendLine($"Technisch: {signal.TrendStatus} | {signal.MomentumStatus}");
        sb.AppendLine($"   {signal.VolatilityStatus} | {signal.VolumeStatus}");
        if (ml != null) sb.AppendLine($"ML model: {ml.Probability:P0} kans op succes");
        sb.AppendLine($"Nieuws sentiment: {sentiment:+0.00}");
        if (claude != null) { sb.AppendLine($"Claude ({claude.Confidence:P0} zekerheid): {claude.Reasoning}"); if (claude.KeyFactors.Length > 0) sb.AppendLine($"   Factors: {string.Join(" | ", claude.KeyFactors)}"); }
        sb.AppendLine($"{signal.Timestamp:dd-MM HH:mm} UTC");
        return sb.ToString();
    }
}
