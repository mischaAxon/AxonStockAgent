namespace AxonStockAgent.Api.Data.Entities;

public class SignalEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Direction { get; set; } = "";
    public double TechScore { get; set; }
    public float? MlProbability { get; set; }
    public double? SentimentScore { get; set; }
    public double? ClaudeConfidence { get; set; }
    public string? ClaudeDirection { get; set; }
    public string? ClaudeReasoning { get; set; }
    public double FinalScore { get; set; }
    public string FinalVerdict { get; set; } = "";
    public double PriceAtSignal { get; set; }
    public string? TrendStatus { get; set; }
    public string? MomentumStatus { get; set; }
    public string? VolatilityStatus { get; set; }
    public string? VolumeStatus { get; set; }
    public bool Notified { get; set; }
    public DateTime CreatedAt { get; set; }

    // Outcome tracking
    public double? PriceAfter1d { get; set; }
    public double? PriceAfter5d { get; set; }
    public double? PriceAfter20d { get; set; }
    public double? ReturnPct1d { get; set; }
    public double? ReturnPct5d { get; set; }
    public double? ReturnPct20d { get; set; }
    public bool? OutcomeCorrect { get; set; }
}
