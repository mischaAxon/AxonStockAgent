using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Core.Interfaces;

public interface IMarketDataProvider
{
    string Name { get; }
    bool SupportsRealtime { get; }
    string[] SupportedExchanges { get; }
    Task<Candle[]?> GetCandles(string symbol, string resolution, int count);
    Task<string[]> GetSymbols(string exchange);
    Task<Quote?> GetQuote(string symbol);
}
