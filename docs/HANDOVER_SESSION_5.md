# AxonStockAgent — Handover Sessie 5

**Datum:** 11 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_4.md`

---

## Wat is er gedaan in sessie 5

Focus: **stabiliteit, code kwaliteit, testbaarheid — en eerste succesvolle E2E run**

### 1. Shared componenten geëxtraheerd ✅
- `VerdictBadge` en `ScoreBar` stonden gedupliceerd in 5 pagina's (SignalsPage, DashboardPage, StockDetailPage, WatchlistPage, PortfolioPage)
- Geëxtraheerd naar `frontend/src/components/shared/`:
  - `VerdictBadge.tsx` — verdict badge met kleurcodering per verdict
  - `ScoreBar.tsx` — score balk met optionele `width` prop (default: `flex-1`)
  - `index.ts` — barrel export
- **119 regels duplicatie verwijderd** uit 5 pagina's
- `ScoreBar` heeft `width` prop: `w-20` in DashboardPage, `w-16` in StockDetailPage, `flex-1` (default) elders

### 2. Server-side tijdfilter op Signals API ✅
- **Backend:** `since` query parameter toegevoegd aan `SignalsController.GetAll()`
  - Type: `DateTime?`, converteert naar UTC met `.ToUniversalTime()`
  - Filtert vóór `CountAsync()` — dus `meta.total` is correct gefilterd
- **Frontend hook:** `useSignals` accepteert nu `since?: string` als 5e parameter
  - Wordt meegestuurd als query param en opgenomen in query key voor caching
- **SignalsPage:**
  - `sinceDate()` (returned `Date | null`) → `sinceISO()` (returned `string | undefined`)
  - Client-side `.filter()` volledig verwijderd
  - Paginatie toont nu correcte aantallen bij tijdfilters

### 3. E2E Smoke Test Script ✅
- `scripts/e2e-smoke-test.sh` — bash script dat alle API endpoints test
- Wacht op API met timeout (120s), registreert testuser, test:
  - Health check
  - Auth (register + login + token)
  - Dashboard
  - Watchlist (CRUD: voegt AAPL, MSFT, ASML.AS toe)
  - Signals (incl. `since` parameter)
  - Portfolio (voegt positie toe)
  - Sectors & News (sector-sentiment, trending)
  - Fundamentals
- Kleurgecodeerde output (groen/rood) met samenvatting
- **Resultaat: 19/19 checks passing**

### 4. Runtime fixes tijdens E2E test ✅
Bij het uitvoeren van de smoke test kwamen vier issues aan het licht, alle opgelost:

- **`database/init.sql`** — `__EFMigrationsHistory` kolommen gewijzigd naar snake_case (`migration_id`, `product_version`) zodat ze matchen met wat `UseSnakeCaseNamingConvention()` genereert
- **`SectorService.GetSectorSummary()`** — EF Core kan geen positional record constructors vertalen naar SQL. Fix: eerst projecteren naar anoniem type, dan in-memory mappen naar `SectorSummaryItem`
- **`NewsService.GetTrendingSymbols()`** — zelfde patroon: anoniem type in SQL, mappen naar `TrendingSymbolDto` na `ToListAsync()`
- **`scripts/e2e-smoke-test.sh`** — `displayName` toegevoegd aan register payload, `!` verwijderd uit wachtwoord, fundamentals test accepteert nu 404 (geldig als er geen providers geconfigureerd zijn)

### 5. Worker test gestart — blocker gevonden
- Finnhub provider geactiveerd via admin UI (API key ingevuld, enabled, health check: "Beperkt")
- Worker logs tonen herhaald: `column w.Symbol does not exist` — hint: `Perhaps you meant to reference the column "w.symbol"`
- **Root cause:** `UseSnakeCaseNamingConvention()` mist in zowel `Api/Program.cs` als `Worker/Program.cs` — EF genereert PascalCase kolomnamen (`"Symbol"`) terwijl PostgreSQL snake_case heeft (`symbol`)
- **Fix prompt klaar:** `docs/prompts/05-snake-case-fix.md` — moet als eerste actie in sessie 6 worden uitgevoerd

---

## Gewijzigde bestanden (sessie 5)

| Bestand | Actie |
|---------|-------|
| `frontend/src/components/shared/VerdictBadge.tsx` | **Nieuw** |
| `frontend/src/components/shared/ScoreBar.tsx` | **Nieuw** |
| `frontend/src/components/shared/index.ts` | **Nieuw** |
| `frontend/src/pages/SignalsPage.tsx` | **Gewijzigd** (shared imports + server-side filter) |
| `frontend/src/pages/DashboardPage.tsx` | **Gewijzigd** (shared imports) |
| `frontend/src/pages/StockDetailPage.tsx` | **Gewijzigd** (shared imports) |
| `frontend/src/pages/WatchlistPage.tsx` | **Gewijzigd** (shared imports) |
| `frontend/src/pages/PortfolioPage.tsx` | **Gewijzigd** (shared imports) |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** (since param in useSignals) |
| `src/AxonStockAgent.Api/Controllers/SignalsController.cs` | **Gewijzigd** (since parameter) |
| `scripts/e2e-smoke-test.sh` | **Nieuw** (+ runtime fixes) |
| `database/init.sql` | **Gewijzigd** (snake_case EF migrations tabel) |
| `SectorService` | **Gewijzigd** (EF query fix) |
| `NewsService` | **Gewijzigd** (EF query fix) |

---

## Openstaande bugs

### BUG: Worker — UseSnakeCaseNamingConvention mist
- **Symptoom:** Worker crash: `column w.Symbol does not exist`
- **Oorzaak:** `UseSnakeCaseNamingConvention()` mist in `Program.cs` van zowel API als Worker
- **Fix:** Voeg `.UseSnakeCaseNamingConvention()` toe aan `AddDbContext<AppDbContext>()` in beide `Program.cs` bestanden + controleer of NuGet package `EFCore.NamingConventions` aanwezig is
- **Prompt:** `docs/prompts/05-snake-case-fix.md`
- **Prioriteit:** BLOCKER — moet als eerste worden opgelost in sessie 6

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
| — | Shared componenten extractie | ✅ (sessie 5) |
| — | Server-side tijdfilter | ✅ (sessie 5) |
| — | E2E smoke test (19/19 pass) | ✅ (sessie 5) |
| — | EF Core query fixes (Sector + News) | ✅ (sessie 5) |
| — | Snake_case migrations fix | ✅ (sessie 5) |
| — | Worker UseSnakeCaseNamingConvention | ❌ BLOCKER |

---

## Volgende stappen (sessie 6)

### Prioriteit 0: Blocker fixen (EERSTE ACTIE)
1. **Plak `docs/prompts/05-snake-case-fix.md` in Claude Code** — voegt `UseSnakeCaseNamingConvention()` toe aan API + Worker
2. `docker compose up -d --build worker api` — herstart met fix
3. Check worker logs: `docker compose logs -f worker`
4. Verwacht: "Scan cycle gestart: 3 symbolen" ipv de column error

### Prioriteit 1: Worker testen met echte scan
5. Controleer dat Finnhub provider actief is (admin UI of API)
6. Voeg `IgnoreMarketHours` toe (prompt 04, optioneel als het buiten markturen is)
7. Verifieer dat signalen worden gegenereerd in de database
8. Check dat frontend pagina's live data tonen

### Prioriteit 2: CI/CD
9. GitHub Actions workflow: build + test + Docker image push
10. Container registry setup (GitHub Container Registry of Azure ACR)

### Prioriteit 3: Azure Deployment
11. Azure Container Apps configuratie
12. Custom domain + SSL

### Prioriteit 4: Algoritme verfijning
13. Backtesting framework
14. Per-sector gewichten
15. ML-model trainen op historische signalen

---

## Prompt voor nieuwe chat (sessie 6)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_5.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: Worker fixen en eerste echte scan draaien (zie blocker in handover)
```
