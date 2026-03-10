namespace AxonStockAgent.Core.Interfaces;

public record CompanyProfile(
    string Symbol,
    string Name,
    string Sector,
    string Industry,
    string Country,
    double MarketCap,
    string Logo,
    string WebUrl,
    string Description
);

public record AnalystRating(
    string Symbol,
    int Buy,
    int Hold,
    int Sell,
    int StrongBuy,
    int StrongSell,
    double TargetPriceMean
);

public record FinancialMetrics(
    string Symbol,
    double? PeRatio,
    double? ForwardPe,
    double? PbRatio,
    double? PsRatio,
    double? EvToEbitda,
    double? ProfitMargin,
    double? OperatingMargin,
    double? ReturnOnEquity,
    double? ReturnOnAssets,
    double? RevenueGrowthYoy,
    double? EarningsGrowthYoy,
    double? DebtToEquity,
    double? CurrentRatio,
    double? QuickRatio,
    double? DividendYield,
    double? PayoutRatio,
    double? MarketCap,
    double? Revenue,
    double? NetIncome,
    long? SharesOutstanding,
    DateTime FetchedAt
);

public record InsiderTransaction(
    string Symbol,
    string Name,
    string Relation,
    string TransactionType,
    DateTime Date,
    long Shares,
    double PricePerShare,
    double TotalValue
);

public record PriceTarget(
    string Symbol,
    double TargetHigh,
    double TargetLow,
    double TargetMean,
    double TargetMedian,
    int NumberOfAnalysts,
    DateTime FetchedAt
);

public interface IFundamentalsProvider
{
    string Name { get; }
    Task<CompanyProfile?> GetProfile(string symbol);
    Task<AnalystRating?> GetAnalystRatings(string symbol);
    Task<FinancialMetrics?> GetFinancialMetrics(string symbol);
    Task<InsiderTransaction[]> GetInsiderTransactions(string symbol, int months = 3);
    Task<PriceTarget?> GetPriceTarget(string symbol);
}
