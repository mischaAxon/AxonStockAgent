# AxonStockAgent — Handover Sessie 11

**Datum:** 13 maart 2026
**Repo:** https://github.com/mischaAxon/AxonStockAgent
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)
**Vorige handover:** `docs/HANDOVER_SESSION_10.md`

---

## Wat is er gedaan in sessie 11

Focus: **Data pipeline werkend krijgen — diagnostiek, index-correctie, worker scan, fundamentals, cleanup**

Vijf prompts geschreven en uitgevoerd (27 t/m 31). Het Markets scherm ging van ~5% data coverage naar 100%.

### 1. Data Diagnostics + NL Index Herindeling (Prompt 27) ✅

**Probleem:** Index-samenstelling klopte niet (ABN dubbel, BESI bij verkeerde index, AEX nog op 25 ipv 30). ~95% tiles toonde "—".

**Oplossing:**
- Diagnostics endpoints: `data-health`, `quote-test/{symbol}`, `quote-batch-test`
- Hardcoded NL index data (`DutchIndexData.cs`) met correcte AEX (30), AMX Midcap, AMS Next 20
- `POST /admin/indices/reload-nl` endpoint
- Verbeterde EODHD quote logging + EOD fallback in GetQuote

### 2. Worker: Scan MarketSymbols + Pillar Scores + Scan Trigger (Prompt 28) ✅

**Probleem:** Worker scande alleen Watchlist (bijna leeg). Geen signalen.

**Oplossing:**
- Worker scant nu MarketSymbols via `scan.scan_source` AlgoSetting
- FundamentalsScore + NewsScore worden gevuld bij signaalopslag → pillar dots
- Scan trigger systeem: `POST /admin/scan/trigger` + `GET /admin/scan/status`
- Worker pollt scan_triggers tabel elke minuut

### 3. Fundamentals Bulk Refresh (Prompt 29) ✅

**Probleem:** Fundamentals tab leeg — data werd alleen on-demand opgehaald.

**Oplossing:**
- `POST /admin/fundamentals/refresh-all` — bulk refresh voor alle MarketSymbols
- `GET /admin/fundamentals/status` — toont coverage statistieken
- Automatische wekelijkse refresh op zondagnacht in de Worker

### 4. Quote Coverage + Orphan Cleanup (Prompt 30) ✅

**Probleem:** 24 orphan symbolen in "Overig AS" kolom, bijna allemaal zonder data.

**Oplossing:**
- `GET /admin/symbols/orphans` — identificeer orphans
- `POST /admin/symbols/cleanup-orphans` — deactiveer ze
- `GET /diagnostics/quote-failures` — test welke index-symbolen falen
- 24 orphans + 3 niet-ondersteunde symbolen (CTP, FAGR, WDP) gedeactiveerd

### 5. Fix Laatste Missende Quotes (Prompt 31) ✅

**Probleem:** 5 index-symbolen zonder koers (AD, ADYEN, APAM, VASTN, TKWY).

**Oplossing:**
- `GET /diagnostics/quote-diagnose/{symbol}` — gedetailleerde diagnose per symbool
- QuoteCacheService: SemaphoreSlim max 5 concurrent EODHD calls (was onbeperkt → rate limit)
- EOD fallback quotes: 15 min cache ipv 30 sec
- AD/ADYEN/APAM: opgelost door rate limit fix
- VASTN/TKWY: geen EODHD data, gedeactiveerd

---

## Eindresultaat sessie 11

| Metric | Begin sessie | Eind sessie |
|--------|-------------|-------------|
| Actieve symbolen | 98 (veel orphans) | 69 (schoon) |
| Symbolen met koers | ~5 (5%) | 69 (100%) |
| Index-kolommen | 3 (incorrect) | 3 (AEX/AMS Next 20/AMX Midcap) |
| "Overig" kolom | 24 symbolen zonder data | Verdwenen |
| Signalen | 0-1 | Worden gegenereerd per scan |
| Fundamentals | 0 | Bulk refresh beschikbaar |
| Pillar scores | Nooit gevuld | Gevuld bij elke scan |
| Scan trigger | Niet mogelijk | Via admin endpoint |

---

## Gewijzigde bestanden (sessie 11)

| Bestand | Actie | Prompt |
|---------|-------|--------|
| `src/.../Controllers/DiagnosticsController.cs` | **Herschreven** — 6 endpoints | 27, 31 |
| `src/.../Services/DutchIndexData.cs` | **Nieuw** | 27 |
| `src/.../Controllers/AdminController.cs` | **Uitgebreid** — 6 nieuwe endpoints | 27, 28, 29, 30 |
| `src/.../Providers/EodhdProvider.cs` | **Gewijzigd** — EOD fallback, betere logging | 27 |
| `src/.../Services/QuoteCacheService.cs` | **Gewijzigd** — semaphore, EOD cache | 27, 31 |
| `src/.../Entities/ScanTriggerEntity.cs` | **Nieuw** | 28 |
| `src/.../Data/AppDbContext.cs` | **Gewijzigd** — ScanTriggers DbSet | 28 |
| `src/.../Worker/ScreenerWorker.cs` | **Gewijzigd** — MarketSymbols scan, pillars, trigger, fund refresh | 28, 29 |
| `src/.../Services/AlgoSettingsService.cs` | **Gewijzigd** — GetStringAsync, scan_source | 28 |
| `src/.../Services/FundamentalsService.cs` | **Gewijzigd** — RefreshAllMarketSymbolsFundamentals | 29 |
| DB migraties | scan_triggers tabel, scan_source setting | 28 |

---

## API endpoints (nieuw in sessie 11)

| Endpoint | Method | Beschrijving |
|----------|--------|--------------|
| `/diagnostics/data-health` | GET | Data-gezondheid overzicht |
| `/diagnostics/quote-test/{symbol}` | GET | Test quote voor één symbool |
| `/diagnostics/quote-batch-test` | GET | Test batch quotes |
| `/diagnostics/quote-failures` | GET | Alle falende quotes |
| `/diagnostics/quote-diagnose/{symbol}` | GET | Gedetailleerde quote diagnose |
| `/admin/indices/reload-nl` | POST | Herlaad NL indexen |
| `/admin/scan/trigger` | POST | Trigger handmatige scan |
| `/admin/scan/status` | GET | Scan trigger status |
| `/admin/fundamentals/refresh-all` | POST | Bulk fundamentals refresh |
| `/admin/fundamentals/status` | GET | Fundamentals coverage stats |
| `/admin/symbols/orphans` | GET | Toon orphan symbolen |
| `/admin/symbols/cleanup-orphans` | POST | Deactiveer orphans |

---

## Volgende stappen (sessie 12)

### Prioriteit 1: UX Polish
1. Sorteer-opties op Markets (change%, score, sentiment, marktcap)
2. Meer visuele feedback — verdict dots en pillar dots verifiëren na scan
3. Sector-filter op Markets

### Prioriteit 2: Validatie
4. Scan triggeren en verifiëren dat signalen correct op het Markets scherm verschijnen
5. Fundamentals refresh triggeren en StockDetailPage Fundamentals tab valideren
6. Signal accuracy beginnen te evalueren

### Prioriteit 3: CI/CD + Deployment
7. GitHub Actions workflow: build + test + Docker image push
8. Azure Container Apps configuratie
9. Custom domain + SSL

### Prioriteit 4: Geavanceerde features
10. Claude AI bedrijfssamenvatting in Profiel tab
11. Per-pijler gewichten configureerbaar in Admin
12. Knowledge graphs / gerelateerde symbolen

---

## Prompt voor nieuwe chat (sessie 12)

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_11.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
