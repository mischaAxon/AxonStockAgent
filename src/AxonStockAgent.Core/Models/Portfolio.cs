namespace AxonStockAgent.Core.Models;

public record PortfolioPosition(
    string Symbol,
    int Shares,
    double? AvgBuyPrice = null,
    string? Notes = null
);

public record DividendPayment(
    DateTime ExDate,
    DateTime PayDate,
    double Amount,
    string Currency,
    string Symbol
);

public record DividendProfile(
    string Symbol,
    string Exchange,
    double CurrentYield,
    double AnnualDividend,
    double FiveYearCagr,
    DividendGrowthTrend Trend,
    DividendPayment[] History,
    DividendPayment? NextEx,
    DateTime LastUpdated
);

public enum DividendGrowthTrend { Growing, Stable, Declining, Irregular, NoDividend }
