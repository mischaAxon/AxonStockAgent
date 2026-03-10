using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Core.Interfaces;

public interface IMarketDataClient
{
    Task<Candle[]?> GetCandles(string symbol, string resolution, int count);
    Task<string[]> GetSymbols(string exchange);
}

public interface INotificationService
{
    Task SendSignal(ScreenerSignal signal);
    Task SendMessage(string message);
}

public interface ISignalRepository
{
    Task SaveSignal(AiEnrichedSignal signal);
    Task<AiEnrichedSignal[]> GetRecentSignals(int count = 50);
    Task<AiEnrichedSignal[]> GetSignalsForSymbol(string symbol, int count = 20);
}

public interface IWatchlistRepository
{
    Task<string[]> GetActiveSymbols();
    Task AddSymbol(string symbol, string? exchange = null, string? name = null);
    Task RemoveSymbol(string symbol);
}

public interface IPortfolioRepository
{
    Task<PortfolioPosition[]> GetPositions();
    Task UpsertPosition(PortfolioPosition position);
    Task RemovePosition(string symbol);
}
