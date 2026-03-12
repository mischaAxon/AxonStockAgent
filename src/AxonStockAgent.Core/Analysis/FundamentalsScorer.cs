namespace AxonStockAgent.Core.Analysis;

/// <summary>
/// Berekent een genormaliseerde fundamentele score (0-1) op basis van
/// waardering, winstgevendheid, groei, balans-gezondheid en analist-consensus.
/// 0.5 = neutraal, >0.5 = positief, <0.5 = negatief.
/// </summary>
public static class FundamentalsScorer
{
    /// <summary>
    /// Bereken de fundamentele score. Elke sub-score is optioneel;
    /// als een metric ontbreekt wordt die sub-score niet meegeteld.
    /// Retourneert 0.5 (neutraal) als er onvoldoende data is.
    /// </summary>
    public static FundamentalsResult Score(
        double? peRatio,
        double? forwardPe,
        double? pbRatio,
        double? profitMargin,
        double? operatingMargin,
        double? returnOnEquity,
        double? revenueGrowthYoy,
        double? earningsGrowthYoy,
        double? debtToEquity,
        double? currentRatio,
        int? analystBuy,
        int? analystHold,
        int? analystSell,
        int? analystStrongBuy,
        int? analystStrongSell,
        double? targetPriceMean,
        double currentPrice)
    {
        var components = new List<(double score, double weight, string name)>();

        // ── 1. Waardering (lagere P/E en P/B = beter, maar negatief = verliesgevend) ──
        var valScore = ScoreValuation(peRatio, forwardPe, pbRatio);
        if (valScore.HasValue)
            components.Add((valScore.Value, 0.25, "Valuation"));

        // ── 2. Winstgevendheid ──
        var profScore = ScoreProfitability(profitMargin, operatingMargin, returnOnEquity);
        if (profScore.HasValue)
            components.Add((profScore.Value, 0.25, "Profitability"));

        // ── 3. Groei ──
        var growthScore = ScoreGrowth(revenueGrowthYoy, earningsGrowthYoy);
        if (growthScore.HasValue)
            components.Add((growthScore.Value, 0.15, "Growth"));

        // ── 4. Balans-gezondheid ──
        var healthScore = ScoreFinancialHealth(debtToEquity, currentRatio);
        if (healthScore.HasValue)
            components.Add((healthScore.Value, 0.10, "Health"));

        // ── 5. Analist consensus ──
        var analystScore = ScoreAnalystConsensus(
            analystBuy, analystHold, analystSell,
            analystStrongBuy, analystStrongSell);
        if (analystScore.HasValue)
            components.Add((analystScore.Value, 0.15, "Analyst"));

        // ── 6. Price target upside/downside ──
        var targetScore = ScorePriceTarget(targetPriceMean, currentPrice);
        if (targetScore.HasValue)
            components.Add((targetScore.Value, 0.10, "PriceTarget"));

        // ── Gewogen gemiddelde ──
        if (components.Count == 0)
            return new FundamentalsResult(0.5, 0, "Geen data", Array.Empty<string>());

        var totalWeight = components.Sum(c => c.weight);
        var weightedScore = components.Sum(c => c.score * c.weight) / totalWeight;
        var finalScore = Clamp(weightedScore, 0, 1);

        var details = components
            .Select(c => $"{c.name}={c.score:F2}")
            .ToArray();

        var desc = finalScore switch
        {
            > 0.70 => "Strong Fundamentals",
            > 0.55 => "Good Fundamentals",
            > 0.45 => "Neutral Fundamentals",
            > 0.30 => "Weak Fundamentals",
            _ => "Poor Fundamentals"
        };

        return new FundamentalsResult(finalScore, components.Count, desc, details);
    }

    // ── Sub-scorers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Waardering: P/E en P/B. Lage waarden zijn beter (ondergewaardeerd),
    /// maar negatieve P/E = verliesgevend = slecht.
    /// </summary>
    private static double? ScoreValuation(double? pe, double? forwardPe, double? pb)
    {
        var scores = new List<double>();

        // P/E: ideaal 10-20, >40 is duur, <0 is verliesgevend
        var peVal = forwardPe ?? pe;  // Forward P/E heeft voorkeur
        if (peVal.HasValue)
        {
            if (peVal.Value < 0)
                scores.Add(0.2);  // verliesgevend
            else
                scores.Add(Clamp(1.0 - (peVal.Value - 15) / 50, 0.1, 0.9));
        }

        // P/B: ideaal 1-3, >5 is duur
        if (pb.HasValue && pb.Value > 0)
        {
            scores.Add(Clamp(1.0 - (pb.Value - 2) / 10, 0.1, 0.9));
        }

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Winstgevendheid: hogere margins en ROE zijn beter.
    /// </summary>
    private static double? ScoreProfitability(double? profitMargin, double? opMargin, double? roe)
    {
        var scores = new List<double>();

        // Profit margin: >20% is excellent, <0% is verliesgevend
        if (profitMargin.HasValue)
            scores.Add(Clamp(0.5 + profitMargin.Value / 40, 0.1, 0.9));

        // Operating margin: >25% is excellent
        if (opMargin.HasValue)
            scores.Add(Clamp(0.5 + opMargin.Value / 50, 0.1, 0.9));

        // ROE: >15% is goed, >25% is excellent
        if (roe.HasValue)
            scores.Add(Clamp(0.5 + roe.Value / 40, 0.1, 0.9));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Groei: positieve groei YoY is goed, negatief is slecht.
    /// </summary>
    private static double? ScoreGrowth(double? revenueGrowth, double? earningsGrowth)
    {
        var scores = new List<double>();

        // Revenue groei: >10% is goed, >25% is excellent
        if (revenueGrowth.HasValue)
            scores.Add(Clamp(0.5 + revenueGrowth.Value / 50, 0.15, 0.85));

        // Earnings groei: meer volatiel, bredere range
        if (earningsGrowth.HasValue)
            scores.Add(Clamp(0.5 + earningsGrowth.Value / 80, 0.15, 0.85));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Financiële gezondheid: lage schuld en gezonde current ratio.
    /// </summary>
    private static double? ScoreFinancialHealth(double? debtToEquity, double? currentRatio)
    {
        var scores = new List<double>();

        // D/E: <1 is gezond, >2 is risicovol
        if (debtToEquity.HasValue && debtToEquity.Value >= 0)
            scores.Add(Clamp(1.0 - debtToEquity.Value / 4, 0.1, 0.9));

        // Current ratio: >1.5 is gezond, <1 is risicovol
        if (currentRatio.HasValue && currentRatio.Value > 0)
            scores.Add(Clamp(currentRatio.Value / 3, 0.1, 0.9));

        return scores.Count > 0 ? scores.Average() : null;
    }

    /// <summary>
    /// Analist consensus: meer buy/strongbuy vs sell/strongsell.
    /// </summary>
    private static double? ScoreAnalystConsensus(
        int? buy, int? hold, int? sell, int? strongBuy, int? strongSell)
    {
        var total = (buy ?? 0) + (hold ?? 0) + (sell ?? 0) + (strongBuy ?? 0) + (strongSell ?? 0);
        if (total == 0) return null;

        // Gewogen score: strongBuy=2, buy=1, hold=0, sell=-1, strongSell=-2
        var weighted = (strongBuy ?? 0) * 2.0 + (buy ?? 0) * 1.0
                     + (sell ?? 0) * -1.0 + (strongSell ?? 0) * -2.0;
        var maxPossible = total * 2.0;  // als iedereen strongBuy zou zeggen

        // Normaliseer naar 0-1
        return Clamp((weighted / maxPossible + 1) / 2, 0.1, 0.9);
    }

    /// <summary>
    /// Price target: hoeveel upside zien analisten?
    /// </summary>
    private static double? ScorePriceTarget(double? targetMean, double currentPrice)
    {
        if (!targetMean.HasValue || currentPrice <= 0 || targetMean.Value <= 0)
            return null;

        var upside = (targetMean.Value - currentPrice) / currentPrice;
        // +20% upside → 0.7, 0% → 0.5, -20% downside → 0.3
        return Clamp(0.5 + upside * 1.0, 0.15, 0.85);
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));
}

public record FundamentalsResult(
    double Score,
    int ComponentCount,
    string Description,
    string[] Details
);
