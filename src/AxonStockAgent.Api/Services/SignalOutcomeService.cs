using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Vult outcome-velden in voor bestaande signalen.
/// Kijkt hoeveel tradingdagen er verstreken zijn sinds het signaal
/// en haalt de huidige prijs op om return te berekenen.
/// </summary>
public class SignalOutcomeService
{
    private readonly AppDbContext _db;
    private readonly ProviderManager _providers;
    private readonly ILogger<SignalOutcomeService> _logger;

    public SignalOutcomeService(AppDbContext db, ProviderManager providers, ILogger<SignalOutcomeService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Verwerk alle signalen die outcome-updates nodig hebben.
    /// Een signaal heeft een update nodig als:
    /// - PriceAfter1d is null EN signaal is >= 1 tradingdag oud
    /// - PriceAfter5d is null EN signaal is >= 5 tradingdagen oud
    /// - PriceAfter20d is null EN signaal is >= 20 tradingdagen oud
    /// </summary>
    public async Task<int> ProcessOutcomesAsync(CancellationToken ct = default)
    {
        var provider = await _providers.GetMarketDataProvider();
        if (provider == null)
        {
            _logger.LogWarning("Geen market data provider beschikbaar voor outcome tracking");
            return 0;
        }

        var cutoff1d  = DateTime.UtcNow.AddDays(-2);    // minstens 1 tradingdag
        var cutoff5d  = DateTime.UtcNow.AddDays(-8);    // minstens 5 tradingdagen (+ weekend)
        var cutoff20d = DateTime.UtcNow.AddDays(-30);   // minstens 20 tradingdagen (+ weekenden)

        var signals = await _db.Signals
            .Where(s =>
                (s.PriceAfter1d == null && s.CreatedAt <= cutoff1d) ||
                (s.PriceAfter5d == null && s.CreatedAt <= cutoff5d) ||
                (s.PriceAfter20d == null && s.CreatedAt <= cutoff20d))
            .OrderBy(s => s.CreatedAt)
            .Take(50)  // batch-limiet om API rate limits te respecteren
            .ToListAsync(ct);

        if (signals.Count == 0) return 0;

        // Groepeer per symbool om candles maar 1x per symbool op te halen
        var symbolGroups = signals.GroupBy(s => s.Symbol);
        int updated = 0;

        foreach (var group in symbolGroups)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var symbol = group.Key;
                var candles = await provider.GetCandles(symbol, "D", 60);
                if (candles == null || candles.Length < 2)
                {
                    _logger.LogDebug("Onvoldoende candles voor outcome tracking van {Symbol}", symbol);
                    continue;
                }

                foreach (var signal in group)
                {
                    updated += UpdateSignalOutcome(signal, candles) ? 1 : 0;
                }

                await Task.Delay(500, ct);  // rate limiting
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outcome tracking mislukt voor {Symbol}", group.Key);
            }
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Outcome tracking: {Updated} signalen bijgewerkt", updated);
        }

        return updated;
    }

    /// <summary>
    /// Vul de outcome-velden in voor één signaal op basis van candle data.
    /// Retourneert true als er iets gewijzigd is.
    /// </summary>
    private bool UpdateSignalOutcome(SignalEntity signal, Candle[] candles)
    {
        bool changed = false;
        var signalDate = signal.CreatedAt;
        var basePrice = signal.PriceAtSignal;
        if (basePrice <= 0) return false;

        // 1 dag: zoek candle ~1 tradingdag na signaal
        if (signal.PriceAfter1d == null)
        {
            var targetDate = AddTradingDays(signalDate, 1);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter1d = price.Value;
                signal.ReturnPct1d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // 5 dagen
        if (signal.PriceAfter5d == null)
        {
            var targetDate = AddTradingDays(signalDate, 5);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter5d = price.Value;
                signal.ReturnPct5d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // 20 dagen
        if (signal.PriceAfter20d == null)
        {
            var targetDate = AddTradingDays(signalDate, 20);
            var price = FindClosestPrice(candles, targetDate);
            if (price.HasValue)
            {
                signal.PriceAfter20d = price.Value;
                signal.ReturnPct20d = (price.Value - basePrice) / basePrice * 100;
                changed = true;
            }
        }

        // Bepaal OutcomeCorrect op basis van de langste beschikbare return
        var longestReturn = signal.ReturnPct20d ?? signal.ReturnPct5d ?? signal.ReturnPct1d;
        if (longestReturn.HasValue)
        {
            signal.OutcomeCorrect = signal.FinalVerdict switch
            {
                "BUY" or "SQUEEZE" => longestReturn.Value > 0,
                "SELL" => longestReturn.Value < 0,
                _ => null
            };
        }

        return changed;
    }

    /// <summary>
    /// Voeg N tradingdagen toe (skip weekenden).
    /// </summary>
    private static DateTime AddTradingDays(DateTime start, int days)
    {
        var date = start;
        int added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                added++;
        }
        return date;
    }

    /// <summary>
    /// Zoek de close-prijs van de candle die het dichtst bij targetDate ligt.
    /// Retourneert null als de data nog niet beschikbaar is.
    /// </summary>
    private static double? FindClosestPrice(Candle[] candles, DateTime targetDate)
    {
        var candidate = candles
            .Where(c => c.Time >= targetDate.Date)
            .OrderBy(c => c.Time)
            .FirstOrDefault();

        if (candidate == default) return null;

        // Accepteer candles tot 5 dagen na target (voor feestdagen etc.)
        if ((candidate.Time - targetDate).TotalDays > 5) return null;

        return candidate.Close;
    }
}
