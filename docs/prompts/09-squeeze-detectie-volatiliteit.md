# Prompt 09 — Squeeze-detectie verbeteren + Volatiliteit als risicofactor

**Status:** ✅ Voltooid  
**Sessie:** 7  
**Datum:** 12 maart 2026

## Samenvatting

Squeeze-detectie vervangen door robuust BB-width percentile ranking systeem en volatiliteit omgekeerd naar risicofactor.

## Wijzigingen

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Core/Models/Signal.cs` | **Gewijzigd** — `IndicatorResult` uitgebreid met `BbWidthPercentile`, `SqueezeBarCount`, `VolatilityRiskMultiplier` |
| `src/AxonStockAgent.Core/Analysis/IndicatorEngine.cs` | **Gewijzigd** — BB-width percentile ranking (120-bar lookback), geïnverteerde volScore, consecutive squeeze bar counting, volatility risk multiplier (0.70–1.0) |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — 4 nieuwe algo_settings ophalen, risk multiplier toepassen op finalScore, squeeze verdict condition aangepast, uitgebreide debug logging |
| `database/init.sql` | **Gewijzigd** — 4 nieuwe algo_settings: `squeeze_min_bars`, `squeeze_percentile`, `volatility_risk_enabled`, `bb_width_lookback` |
| `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` | **Gewijzigd** — seed defaults uitgebreid met 4 nieuwe settings |

## Ontwerpkeuzes

- **Percentile ranking** i.p.v. absolute drempels: elke stock heeft eigen "normale" BB-width, relatieve vergelijking is eerlijker
- **Risk multiplier als aparte laag** bovenop gewogen score: volatiliteit is zowel signaal (compressie → squeeze) als risicofactor (hoge vol → onbetrouwbaar)
- **≥3 bars in squeeze vereist**: voorkomt false positives door ruis, configureerbaar via `squeeze_min_bars`
- **Geïnverteerde volScore**: lage volatiliteit = stabiel = positief, hoge volatiliteit = risico = negatief
