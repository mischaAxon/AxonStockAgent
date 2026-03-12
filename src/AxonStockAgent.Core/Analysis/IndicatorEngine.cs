using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Core.Analysis;

/// <summary>
/// Technische analyse engine: berekent trend, momentum, volatiliteit en volume
/// indicatoren uit ruwe candle data en normaliseert ze naar een -1 tot +1 score.
/// </summary>
public static class IndicatorEngine
{
    /// <summary>
    /// Analyseer een reeks candles en retourneer een genormaliseerd IndicatorResult.
    /// Vereist minimaal 50 candles voor betrouwbare resultaten.
    /// </summary>
    public static IndicatorResult Analyze(Candle[] candles)
    {
        if (candles.Length < 50)
            throw new ArgumentException($"Minimaal 50 candles vereist, {candles.Length} ontvangen");

        var closes = candles.Select(c => c.Close).ToArray();
        var highs = candles.Select(c => c.High).ToArray();
        var lows = candles.Select(c => c.Low).ToArray();
        var volumes = candles.Select(c => (double)c.Volume).ToArray();

        // ── Trend (EMA crossover + prijs vs SMA) ──
        var ema20 = Ema(closes, 20);
        var ema50 = Ema(closes, 50);
        var sma200 = Sma(closes, 200);

        double trendScore = 0;
        string trendDesc;

        // EMA20 vs EMA50 crossover
        if (ema20.Length >= 2 && ema50.Length >= 2)
        {
            var currentSpread = (ema20[^1] - ema50[^1]) / ema50[^1];
            var prevSpread = (ema20[^2] - ema50[^2]) / ema50[^2];
            trendScore += Clamp(currentSpread * 10, -0.5, 0.5);

            // Crossover bonus
            if (prevSpread <= 0 && currentSpread > 0) trendScore += 0.3;  // golden cross
            if (prevSpread >= 0 && currentSpread < 0) trendScore -= 0.3;  // death cross
        }

        // Prijs vs SMA200
        if (sma200.Length > 0)
        {
            var priceTo200 = (closes[^1] - sma200[^1]) / sma200[^1];
            trendScore += Clamp(priceTo200 * 5, -0.3, 0.3);
        }

        trendScore = Clamp(trendScore, -1, 1);
        trendDesc = trendScore switch
        {
            > 0.5 => "Strong Uptrend",
            > 0.15 => "Uptrend",
            > -0.15 => "Neutral",
            > -0.5 => "Downtrend",
            _ => "Strong Downtrend"
        };

        // ── Momentum (RSI + MACD) ──
        var rsi = Rsi(closes, 14);
        var (macdLine, signalLine, histogram) = Macd(closes);

        double momentumScore = 0;
        string momDesc;

        if (rsi.Length > 0)
        {
            var currentRsi = rsi[^1];
            momentumScore += Clamp((currentRsi - 50) / 50, -0.5, 0.5);
        }

        if (histogram.Length >= 2)
        {
            var macdTrend = histogram[^1] - histogram[^2];
            momentumScore += Clamp(macdTrend * 20, -0.5, 0.5);
        }

        momentumScore = Clamp(momentumScore, -1, 1);
        momDesc = momentumScore switch
        {
            > 0.5 => "Strong Bullish Momentum",
            > 0.15 => "Bullish Momentum",
            > -0.15 => "Neutral Momentum",
            > -0.5 => "Bearish Momentum",
            _ => "Strong Bearish Momentum"
        };

        // ── Volatiliteit (ATR-based risk + BB-width percentile squeeze) ──
        var atr = Atr(highs, lows, closes, 14);
        var bbWidth = BollingerBandWidth(closes, 20, 2);

        double volScore = 0;
        string volDesc;
        double bbWidthPercentile = 0.5;
        int squeezeBarCount = 0;
        double volatilityRiskMultiplier = 1.0;

        if (atr.Length > 0)
        {
            // ATR als percentage van de prijs — hogere ATR% = LAGERE score (risico)
            var atrPct = atr[^1] / closes[^1];
            // Inverteer: lage ATR% → score richting +1, hoge ATR% → score richting -1
            // Baseline 2% ATR is neutraal
            volScore += Clamp((0.02 - atrPct) * 25, -0.5, 0.5);
        }

        if (bbWidth.Length >= 20)
        {
            // ── BB-width percentile ranking ──
            var lookback = Math.Min(bbWidth.Length, 120);
            var recentWidths = bbWidth.TakeLast(lookback).ToArray();
            var currentWidth = bbWidth[^1];
            var countBelow = recentWidths.Count(w => w < currentWidth);
            bbWidthPercentile = (double)countBelow / recentWidths.Length;

            // BB-width verandering bijdrage (compressie = positief voor squeeze, expansie = negatief)
            var widthChange = (bbWidth[^1] - bbWidth[^2]) / bbWidth[^2];
            volScore += Clamp(-widthChange * 10, -0.5, 0.5);

            // ── Squeeze detectie: BB-width in laagste 20% van lookback ──
            const double squeezePercentileThreshold = 0.20;
            if (bbWidthPercentile <= squeezePercentileThreshold)
            {
                squeezeBarCount = 1;
                for (int i = recentWidths.Length - 2; i >= 0; i--)
                {
                    var barPercentile = (double)recentWidths.Take(i + 1).Count(w => w < recentWidths[i]) / (i + 1);
                    if (barPercentile <= squeezePercentileThreshold)
                        squeezeBarCount++;
                    else
                        break;
                }
            }

            // ── Volatility risk multiplier ──
            // Hoge volatiliteit (brede BB) = lagere multiplier = lagere eindscore
            if (bbWidthPercentile <= 0.3)
                volatilityRiskMultiplier = 1.0;
            else if (bbWidthPercentile <= 0.7)
                volatilityRiskMultiplier = 1.0 - (bbWidthPercentile - 0.3) * 0.375; // 1.0 → 0.85
            else
                volatilityRiskMultiplier = 0.85 - (bbWidthPercentile - 0.7) * 0.5;  // 0.85 → 0.70

            volatilityRiskMultiplier = Clamp(volatilityRiskMultiplier, 0.70, 1.0);
        }
        else if (bbWidth.Length >= 2)
        {
            // Fallback als er minder dan 20 bars BB-width zijn
            var widthChange = (bbWidth[^1] - bbWidth[^2]) / bbWidth[^2];
            volScore += Clamp(-widthChange * 10, -0.5, 0.5);
        }

        volScore = Clamp(volScore, -1, 1);
        volDesc = volScore switch
        {
            > 0.5  => "Low Volatility (Stable)",
            > 0.15 => "Below Average Volatility",
            > -0.15 => "Normal Volatility",
            > -0.5 => "Above Average Volatility",
            _ => "High Volatility (Risk)"
        };

        bool squeezeDetected = squeezeBarCount >= 3 && Math.Abs(momentumScore) > 0.15;

        // ── Volume ──
        double volumeScore = 0;
        string volumeDesc;

        if (volumes.Length >= 20)
        {
            var avgVol20 = volumes.TakeLast(20).Average();
            var currentVol = volumes[^1];
            var volRatio = currentVol / avgVol20;
            volumeScore = Clamp((volRatio - 1.0) * 0.5, -1, 1);
        }

        volumeDesc = volumeScore switch
        {
            > 0.5 => "Very High Volume",
            > 0.15 => "Above Average Volume",
            > -0.15 => "Normal Volume",
            > -0.5 => "Below Average Volume",
            _ => "Very Low Volume"
        };

        // ── Genormaliseerde score ──
        var normScore = (trendScore * 0.35) + (momentumScore * 0.30) +
                        (volScore * 0.15) + (volumeScore * 0.20);

        return new IndicatorResult(
            TrendScore: trendScore,
            MomentumScore: momentumScore,
            VolatilityScore: volScore,
            VolumeScore: volumeScore,
            NormScore: normScore,
            TrendDesc: trendDesc,
            MomDesc: momDesc,
            VolDesc: volDesc,
            VolumDesc: volumeDesc,
            SqueezeDetected: squeezeDetected,
            BbWidthPercentile: bbWidthPercentile,
            SqueezeBarCount: squeezeBarCount,
            VolatilityRiskMultiplier: volatilityRiskMultiplier
        );
    }

    // ── Indicator berekeningen ─────────────────────────────────────────────────

    private static double[] Ema(double[] data, int period)
    {
        if (data.Length < period) return Array.Empty<double>();
        var result = new double[data.Length - period + 1];
        var multiplier = 2.0 / (period + 1);

        result[0] = data.Take(period).Average();

        for (int i = 1; i < result.Length; i++)
        {
            result[i] = (data[period + i - 1] - result[i - 1]) * multiplier + result[i - 1];
        }
        return result;
    }

    private static double[] Sma(double[] data, int period)
    {
        if (data.Length < period) return Array.Empty<double>();
        var result = new double[data.Length - period + 1];
        var sum = data.Take(period).Sum();
        result[0] = sum / period;

        for (int i = 1; i < result.Length; i++)
        {
            sum += data[period + i - 1] - data[i - 1];
            result[i] = sum / period;
        }
        return result;
    }

    private static double[] Rsi(double[] closes, int period = 14)
    {
        if (closes.Length < period + 1) return Array.Empty<double>();

        var gains = new double[closes.Length - 1];
        var losses = new double[closes.Length - 1];

        for (int i = 0; i < closes.Length - 1; i++)
        {
            var change = closes[i + 1] - closes[i];
            gains[i] = Math.Max(change, 0);
            losses[i] = Math.Max(-change, 0);
        }

        var result = new double[closes.Length - period];
        var avgGain = gains.Take(period).Average();
        var avgLoss = losses.Take(period).Average();

        result[0] = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));

        for (int i = 1; i < result.Length; i++)
        {
            avgGain = (avgGain * (period - 1) + gains[period + i - 1]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[period + i - 1]) / period;
            result[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));
        }
        return result;
    }

    private static (double[] macd, double[] signal, double[] histogram) Macd(
        double[] closes, int fast = 12, int slow = 26, int signal = 9)
    {
        var emaFast = Ema(closes, fast);
        var emaSlow = Ema(closes, slow);

        if (emaFast.Length == 0 || emaSlow.Length == 0)
            return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());

        var offset = emaFast.Length - emaSlow.Length;
        var macdLine = new double[emaSlow.Length];
        for (int i = 0; i < macdLine.Length; i++)
            macdLine[i] = emaFast[i + offset] - emaSlow[i];

        var signalLine = Ema(macdLine, signal);
        if (signalLine.Length == 0)
            return (macdLine, Array.Empty<double>(), Array.Empty<double>());

        var histOffset = macdLine.Length - signalLine.Length;
        var histogram = new double[signalLine.Length];
        for (int i = 0; i < histogram.Length; i++)
            histogram[i] = macdLine[i + histOffset] - signalLine[i];

        return (macdLine, signalLine, histogram);
    }

    private static double[] Atr(double[] highs, double[] lows, double[] closes, int period = 14)
    {
        if (closes.Length < period + 1) return Array.Empty<double>();

        var tr = new double[closes.Length - 1];
        for (int i = 1; i < closes.Length; i++)
        {
            tr[i - 1] = Math.Max(
                highs[i] - lows[i],
                Math.Max(
                    Math.Abs(highs[i] - closes[i - 1]),
                    Math.Abs(lows[i] - closes[i - 1])
                ));
        }

        var result = new double[tr.Length - period + 1];
        result[0] = tr.Take(period).Average();
        for (int i = 1; i < result.Length; i++)
        {
            result[i] = (result[i - 1] * (period - 1) + tr[period + i - 1]) / period;
        }
        return result;
    }

    private static double[] BollingerBandWidth(double[] closes, int period = 20, double mult = 2)
    {
        var sma = Sma(closes, period);
        if (sma.Length == 0) return Array.Empty<double>();

        var result = new double[sma.Length];
        for (int i = 0; i < sma.Length; i++)
        {
            var slice = closes.Skip(i).Take(period).ToArray();
            var mean = sma[i];
            var stdDev = Math.Sqrt(slice.Average(v => Math.Pow(v - mean, 2)));
            var upper = mean + mult * stdDev;
            var lower = mean - mult * stdDev;
            result[i] = (upper - lower) / mean;
        }
        return result;
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));
}
