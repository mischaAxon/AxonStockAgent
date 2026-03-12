namespace AxonStockAgent.Core.Models;

public class Quote
{
    public string Symbol { get; set; } = "";
    public double CurrentPrice { get; set; }
    public double PreviousClose { get; set; }
    public double Change { get; set; }
    public double ChangePercent { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Open { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
}
