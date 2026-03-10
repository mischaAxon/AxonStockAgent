using System;

namespace AxonStockAgent.Api.Data.Entities;

public class CompanyFundamentalsEntity
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;

    // Valuation
    public double? PeRatio { get; set; }
    public double? ForwardPe { get; set; }
    public double? PbRatio { get; set; }
    public double? PsRatio { get; set; }
    public double? EvToEbitda { get; set; }

    // Profitability
    public double? ProfitMargin { get; set; }
    public double? OperatingMargin { get; set; }
    public double? ReturnOnEquity { get; set; }
    public double? ReturnOnAssets { get; set; }

    // Growth
    public double? RevenueGrowthYoy { get; set; }
    public double? EarningsGrowthYoy { get; set; }

    // Balance sheet
    public double? DebtToEquity { get; set; }
    public double? CurrentRatio { get; set; }
    public double? QuickRatio { get; set; }

    // Dividends
    public double? DividendYield { get; set; }
    public double? PayoutRatio { get; set; }

    // Size
    public double? MarketCap { get; set; }
    public double? Revenue { get; set; }
    public double? NetIncome { get; set; }
    public long? SharesOutstanding { get; set; }

    // Analyst
    public int? AnalystBuy { get; set; }
    public int? AnalystHold { get; set; }
    public int? AnalystSell { get; set; }
    public int? AnalystStrongBuy { get; set; }
    public int? AnalystStrongSell { get; set; }
    public double? TargetPriceHigh { get; set; }
    public double? TargetPriceLow { get; set; }
    public double? TargetPriceMean { get; set; }
    public double? TargetPriceMedian { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
