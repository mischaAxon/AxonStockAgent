# AxonStockAgent — Handover Sessie 7

**Datum:** 12 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_6.md`

---

## Wat is er gedaan in sessie 7

Focus: **Alle open bevindingen uit de sessie 6 projectscan oplossen + candle-caching**

Drie prompts uitgevoerd, allemaal gericht op betrouwbaarheid en observability van het scansysteem.

### 1. Squeeze-detectie + volatiliteit als risicofactor (Prompt 09) ✅

**Probleem:** Squeeze-detectie was fragiel (binaire check op `volScore < -0.3 && momentum > 0.2`). Volatiliteitsscore beloonde hoge volatiliteit terwijl dat juist een risicofactor is.

**Oplossing:**
- BB-width percentile ranking over 120 bars lookback: squeeze wordt relatief aan het aandeel zelf gedetecteerd
- Opeenvolgende squeeze bars tellen: minstens 3 bars in het laagste 20%-percentiel vereist (configureerbaar)
- Volatiliteitsscore geïnverteerd: lage vol = stabiel = positief, hoge vol = risico = negatief
- Volatility Risk Multiplier (0.70–1.0): aparte laag bovenop de gewogen eindscore
- `IndicatorResult` uitgebreid met `BbWidthPercentile`, `SqueezeBarCount`, `VolatilityRiskMultiplier`
- 4 nieuwe `algo_settings`: `squeeze_min_bars`, `squeeze_percentile`, `volatility_risk_enabled`, `bb_width_lookback`

### 2. Candle-caching in Worker (Prompt 10) ✅

**Probleem:** Elke scan cycle haalde candles opnieuw op via de externe API voor alle symbolen, ook als de data niet veranderd was. Problematisch met Finnhub rate limits (60 calls/min).

**Oplossing:**
- Nieuw: `CandleCacheService` — Singleton met `IMemoryCache`
- EOD mode: 24 uur TTL (data verandert niet na market close)
- Realtime mode: 5 minuten TTL (laatste bar kan veranderen)
- `SizeLimit` van 50.000 entries als geheugen-vangnet
- Transparant: Worker roept cache aan i.p.v. direct de provider
- Geen Redis nodig: single-process, kleine data, hoeft herstart niet te overleven

### 3. Claude parsing failure logging naar DB (Prompt 11) ✅

**Probleem:** Als Claude API calls faalden of responses niet geparsed konden worden, was dit alleen zichtbaar in console logs. Geen structureel inzicht in betrouwbaarheid van de Claude-integratie.

**Oplossing:**
- Nieuwe tabel: `claude_api_logs` met status, HTTP code, duration_ms, error_message, raw_response_snippet, model
- `ClaudeAnalysisService` volledig herschreven met `Stopwatch` timing en `LogInteractionAsync` na elke uitkomst
- 6 statustypen: `success`, `api_error`, `parse_error`, `empty_response`, `timeout`, `unknown`
- Nullable `IServiceScopeFactory` voor DB-logging (backward compatible)
- Nieuw admin endpoint: `GET /api/v1/diagnostics/claude/stats?days=30` — success rate, breakdown per status/symbool, latency, recente errors

---

## Gewijzigde bestanden (sessie 7)

| Bestand | Actie | Prompt |
|---------|-------|--------|
| `src/AxonStockAgent.Core/Models/Signal.cs` | **Gewijzigd** — 3 nieuwe velden op IndicatorResult | 09 |
| `src/AxonStockAgent.Core/Analysis/IndicatorEngine.cs` | **Gewijzigd** — BB-width percentile, geïnverteerde volScore, risk multiplier | 09 |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — 4 nieuwe settings, risk multiplier, candle cache, scopeFactory voor Claude | 09, 10, 11 |
| `database/init.sql` | **Gewijzigd** — 4 squeeze/vol settings + claude_api_logs tabel | 09, 11 |
| `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` | **Gewijzigd** — seed defaults uitgebreid | 09 |
| `src/AxonStockAgent.Worker/Services/CandleCacheService.cs` | **Nieuw** — IMemoryCache wrapper met dynamische TTL | 10 |
| `src/AxonStockAgent.Worker/Program.cs` | **Gewijzigd** — AddMemoryCache + CandleCacheService Singleton | 10 |
| `src/AxonStockAgent.Worker/Services/ClaudeAnalysisService.cs` | **Herschreven** — Stopwatch, DB logging, nullable scopeFactory | 11 |
| `src/AxonStockAgent.Api/Data/Entities/ClaudeApiLogEntity.cs` | **Nieuw** — Entity voor Claude API logs | 11 |
| `src/AxonStockAgent.Api/Data/AppDbContext.cs` | **Gewijzigd** — ClaudeApiLogs DbSet + config | 11 |
| `src/AxonStockAgent.Api/Controllers/DiagnosticsController.cs` | **Nieuw** — Claude stats endpoint (admin-only) | 11 |
| `docs/prompts/09-squeeze-detectie-volatiliteit.md` | **Nieuw** | — |
| `docs/prompts/10-candle-caching-worker.md` | **Nieuw** | — |
| `docs/prompts/11-claude-parsing-failure-logging.md` | **Nieuw** | — |

---

## Status open bevindingen uit sessie 6

| # | Bevinding | Status |
|---|-----------|--------|
| 1 | Squeeze-detectie fragiel | ✅ Opgelost — prompt 09 |
| 2 | Volatiliteitsscore beloont hoge vol | ✅ Opgelost — prompt 09 |
| 3 | Geen candle-caching in Worker | ✅ Opgelost — prompt 10 |
| 4 | Volume score kan matig signaal over threshold duwen | ℹ️ Configureerbaar via admin (gewichten) |
| 5 | Claude parsing failure logging ontbreekt | ✅ Opgelost — prompt 11 |
| 6 | Dedup-window zinloos in EOD-mode | ℹ️ Configureerbaar via admin |

Alle 6 bevindingen zijn afgehandeld.

---

## GitHub Issues (Roadmap)

| # | Titel | Status |
|---|-------|--------|
| [#3](https://github.com/mischaAxon/AxonStockAgent/issues/3) | Configureerbaar algoritme | ✅ |
| [#4](https://github.com/mischaAxon/AxonStockAgent/issues/4) | Nieuwsticker + Sector Sentiment | ✅ |
| [#5](https://github.com/mischaAxon/AxonStockAgent/issues/5) | Sector classificatie | ✅ |
| [#6](https://github.com/mischaAxon/AxonStockAgent/issues/6) | Bedrijfsdata verzamelen | ✅ |
| [#7](https://github.com/mischaAxon/AxonStockAgent/issues/7) | Auth + Rollen | ✅ |
| [#8](https://github.com/mischaAxon/AxonStockAgent/issues/8) | Pluggable Provider Systeem | ✅ |
| [#9](https://github.com/mischaAxon/AxonStockAgent/issues/9) | Roadmap — Master bouwvolgorde | Open |
| — | Score-normalisatie fix | ✅ (sessie 6, prompt 06) |
| — | FundamentalsScorer integratie | ✅ (sessie 6, prompt 07) |
| — | Signal outcome tracking | ✅ (sessie 6, prompt 08) |
| — | Squeeze-detectie + volatiliteit | ✅ (sessie 7, prompt 09) |
| — | Candle-caching | ✅ (sessie 7, prompt 10) |
| — | Claude failure logging | ✅ (sessie 7, prompt 11) |

---

## Volgende stappen (sessie 8)

### Prioriteit 1: CI/CD
1. GitHub Actions workflow: build + test + Docker image push
2. Container registry setup (GitHub Container Registry of Azure ACR)

### Prioriteit 2: Azure Deployment
3. Azure Container Apps configuratie
4. Custom domain + SSL
5. Managed PostgreSQL + Redis (Azure Database for PostgreSQL Flexible Server)

### Prioriteit 3: Eerste echte data validatie
6. Worker laten draaien met echte data (EODHD of Finnhub provider actief)
7. Na 1+ dag: check `/api/v1/signals/accuracy?days=30` voor signaal-nauwkeurigheid
8. Check `/api/v1/diagnostics/claude/stats?days=7` voor Claude betrouwbaarheid
9. Evalueer of thresholds en gewichten bijgesteld moeten worden

### Prioriteit 4: Algoritme verdere verfijning
10. Per-sector gewichten (tech vs consumer staples hebben andere volatiliteitsnormen)
11. ML-model trainen op historische signalen (outcome data komt nu binnen)
12. Backtesting framework

### Prioriteit 5: Frontend verbeteringen
13. Claude diagnostics dashboard in de admin UI
14. Signaal accuracy visualisatie (chart met success rate over tijd)

---

## Prompt voor nieuwe chat (sessie 8)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_7.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
