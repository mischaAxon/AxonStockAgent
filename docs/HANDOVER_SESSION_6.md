# AxonStockAgent — Handover Sessie 6

**Datum:** 12 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_5.md`

---

## Wat is er gedaan in sessie 6

Focus: **Totale projectscan op betrouwbaarheid als signaleerder — en 3 kritieke fixes doorvoeren**

De sessie begon met een grondige code-review van de volledige codebase. Hieruit kwamen 10 bevindingen, waarvan de 3 meest impactvolle direct zijn opgelost.

### 1. Score-normalisatie fix (Prompt 06) ✅

**Probleem:** De gewogen eindscore werd berekend door te delen door de som van alleen de *beschikbare* brongewichten. Dit betekende dat ASML.AS (geen sentiment vanwege Finnhub 403) een totaal andere score-schaal had dan AAPL (alles beschikbaar). De BUY/SELL thresholds waren daardoor onbetrouwbaar.

**Oplossing:**
- Nieuwe `algo_settings`: `normalize_missing_sources` (boolean, default `true`)
- Nieuwe `algo_settings`: `signal_dedup_minutes` (integer, default `60`) — was hardcoded, nu configureerbaar
- Dual-mode scoring in `ScanSymbolAsync`:
  - **Normalize mode** (standaard): ontbrekende bronnen → 0.5 (neutraal), gewichten tellen altijd tot 1.0
  - **Legacy mode**: alleen beschikbare bronnen meetellen (oud gedrag, schakelbaar via admin UI)
- Debug logging per symbool met alle score-componenten en welke bronnen aanwezig/neutraal zijn

### 2. FundamentalsScorer geïntegreerd (Prompt 07) ✅

**Probleem:** `fundNorm` was hardcoded op `0.5` (neutraal), terwijl er al fundamentals data in de database zat (P/E, ROE, margins, analist-ratings, price targets via FundamentalsService).

**Oplossing:**
- Nieuw bestand: `src/AxonStockAgent.Core/Analysis/FundamentalsScorer.cs`
- Static scorer met 6 sub-componenten:
  - **Valuation** (25%): P/E, Forward P/E, P/B
  - **Profitability** (25%): profit margin, operating margin, ROE
  - **Growth** (15%): revenue growth YoY, earnings growth YoY
  - **Financial Health** (10%): debt-to-equity, current ratio
  - **Analyst Consensus** (15%): gewogen strongBuy→strongSell
  - **Price Target** (10%): upside/downside vs huidige prijs
- Ontbrekende sub-scores worden overgeslagen (net als IndicatorEngine)
- `FundamentalsService` geregistreerd in Worker DI
- Worker haalt cached fundamentals op (24h TTL) en scoort ze

### 3. Signal Outcome Tracking (Prompt 08) ✅

**Probleem:** Er was geen manier om te meten of signalen achteraf correct waren. Zonder feedback loop is "betrouwbare signaleerder" niet toetsbaar.

**Oplossing:**
- 7 nieuwe velden op `SignalEntity`: `PriceAfter1d`, `PriceAfter5d`, `PriceAfter20d`, `ReturnPct1d`, `ReturnPct5d`, `ReturnPct20d`, `OutcomeCorrect`
- Nieuw: `SignalOutcomeService` — zoekt signalen met ontbrekende outcomes, fetcht candles gegroepeerd per symbool, berekent return % na 1/5/20 tradingdagen
- Nieuw: `OutcomeTrackerService` (BackgroundService) — draait elke 6 uur in de API-container
- Nieuw API endpoint: `GET /api/v1/signals/accuracy?days=30` — nauwkeurigheid per verdict met gemiddelde returns
- Frontend `Signal` type uitgebreid met outcome-velden
- `OutcomeCorrect` logica: BUY/SQUEEZE + positieve return = correct, SELL + negatieve return = correct

---

## Gewijzigde bestanden (sessie 6)

| Bestand | Actie |
|---------|-------|
| `database/init.sql` | **Gewijzigd** — 2 nieuwe algo_settings + 7 ALTER TABLE kolommen voor outcomes |
| `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` | **Gewijzigd** — seed SQL uitgebreid |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — score-normalisatie + fundamentals scoring |
| `src/AxonStockAgent.Worker/Program.cs` | **Gewijzigd** — FundamentalsService in DI |
| `src/AxonStockAgent.Core/Analysis/FundamentalsScorer.cs` | **Nieuw** — 6-componenten fundamentals scorer |
| `src/AxonStockAgent.Api/Data/Entities/SignalEntity.cs` | **Gewijzigd** — 7 outcome properties |
| `src/AxonStockAgent.Api/Services/SignalOutcomeService.cs` | **Nieuw** — outcome berekening |
| `src/AxonStockAgent.Api/BackgroundServices/OutcomeTrackerService.cs` | **Nieuw** — achtergrond-job (6-uurlijk) |
| `src/AxonStockAgent.Api/Program.cs` | **Gewijzigd** — DI registraties |
| `src/AxonStockAgent.Api/Controllers/SignalsController.cs` | **Gewijzigd** — /accuracy endpoint |
| `frontend/src/types/index.ts` | **Gewijzigd** — Signal interface uitgebreid |
| `docs/prompts/06-score-normalisatie.md` | **Nieuw** |
| `docs/prompts/07-fundamentals-scorer.md` | **Nieuw** |
| `docs/prompts/08-signal-outcome-tracking.md` | **Nieuw** |

---

## Bevindingen uit projectscan (niet opgelost in sessie 6)

Deze issues zijn geïdentificeerd maar nog niet aangepakt:

| # | Bevinding | Ernst | Status |
|---|-----------|-------|--------|
| 1 | Squeeze-detectie is fragiel (binaire check op 2 drempels, geen historische BB-width compressie) | Medium | Open — prompt 09 |
| 2 | Volatiliteitsscore beloont hoge volatiliteit (contra-intuitief voor betrouwbaarheid) | Medium | Open — prompt 09 |
| 3 | Geen candle-caching in Worker (elke cycle fetcht alles opnieuw) | Low | Open |
| 4 | Volume score (20%) kan matig technisch signaal over threshold duwen | Low | Configureerbaar via admin |
| 5 | Claude parsing failure logging naar DB ontbreekt (geen zicht op betrouwbaarheid Claude-integratie) | Low | Open |
| 6 | Dedup-window (60 min) is zinloos in EOD-mode (1 scan/dag) | Info | Configureerbaar via admin |

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

---

## Volgende stappen (sessie 7)

### Prioriteit 0: Worker eerste echte scan draaien
1. Controleer dat Finnhub (of EODHD) provider actief is (admin UI)
2. Als je buiten markturen test: schakel `realtime_mode` aan in admin settings, of voeg `IgnoreMarketHours` logica toe
3. `docker compose up -d --build worker api` — herstart met alle fixes
4. Check worker logs: `docker compose logs -f worker`
5. Verifieer dat signalen worden gegenereerd in de database
6. Check `/api/v1/signals/accuracy?days=30` na 1+ dag

### Prioriteit 1: Squeeze-detectie + volatiliteit verfijnen (prompt 09)
7. BB-width compressie over N periodes i.p.v. binaire check
8. Volatiliteitsscore als risicofactor (hoge vol = lagere score)

### Prioriteit 2: CI/CD
9. GitHub Actions workflow: build + test + Docker image push
10. Container registry setup

### Prioriteit 3: Azure Deployment
11. Azure Container Apps configuratie
12. Custom domain + SSL

### Prioriteit 4: Algoritme verdere verfijning
13. Per-sector gewichten
14. ML-model trainen op historische signalen (nu outcome data beschikbaar komt)
15. Backtesting framework

---

## Prompt voor nieuwe chat (sessie 7)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_6.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
