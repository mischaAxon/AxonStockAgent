namespace AxonStockAgent.Core.Models;

public class ScreenerConfig
{
    public string FinnhubApiKey { get; set; } = "";
    public string ClaudeApiKey { get; set; } = "";
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";
    public bool EnableMlModel { get; set; } = true;
    public bool EnableClaudeAnalysis { get; set; } = true;
    public bool EnableSentiment { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 15;
    public int CandleHistoryCount { get; set; } = 100;
    public string Timeframe { get; set; } = "D";
    public double BullThreshold { get; set; } = 0.35;
    public double BearThreshold { get; set; } = -0.35;
    public List<string> Watchlist { get; set; } = new();
}
