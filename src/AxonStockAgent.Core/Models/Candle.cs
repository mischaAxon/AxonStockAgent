namespace AxonStockAgent.Core.Models;

public record Candle(
    DateTime Time,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume
);
