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

public interface IFundamentalsProvider
{
    string Name { get; }
    Task<CompanyProfile?> GetProfile(string symbol);
    Task<AnalystRating?> GetAnalystRatings(string symbol);
}
