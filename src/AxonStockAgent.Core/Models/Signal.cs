namespace AxonStockAgent.Core.Models;

public record ScreenerSignal(
    string Symbol,
    string Exchange,
    string Direction,
    double Score,
    double Price,
    string TrendStatus,
    string MomentumStatus,
    string VolatilityStatus,
    string VolumeStatus,
    DateTime Timestamp
);

public record IndicatorResult(
    double TrendScore,
    double MomentumScore,
    double VolatilityScore,
    double VolumeScore,
    double NormScore,
    string TrendDesc,
    string MomDesc,
    string VolDesc,
    string VolumDesc,
    bool SqueezeDetected
);

public record AiEnrichedSignal(
    ScreenerSignal BaseSignal,
    float? MlProbability,
    double SentimentScore,
    ClaudeAssessment? Claude,
    double FinalScore,
    string FinalVerdict,
    string Summary
);

public record ClaudeAssessment(
    double Confidence,
    string Direction,
    string Reasoning,
    bool NewsConfirms,
    string[] KeyFactors
);

public record NewsItem(
    string Headline,
    double Sentiment,
    DateTime Published,
    string Source
);
