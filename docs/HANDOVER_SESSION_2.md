# AxonStockAgent — Handover Sessie 2

**Datum:** 10 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_1.md`

---

## Wat is er gedaan in sessie 2

### Review + Fix `feature/algo-config` → gemerged
- Gereviewed: 6 bevindingen (weights validatie, JsonSerializerOptions, missende DbSets, TypeScript types, error handling, EF migration)
- Fix-prompt geschreven (`docs/prompts/02-algo-config-fixes.md`), alle fixes uitgevoerd
- Branch gemerged naar main

### Fase 3: Bedrijfsdata (#6) ✅
- **Branch `feature/fundamentals`** → gemerged
- `IFundamentalsProvider` uitgebreid met 3 nieuwe records (FinancialMetrics, InsiderTransaction, PriceTarget) + 3 nieuwe methodes
- `FinnhubProvider` implementeert alle 5 interface-methodes (GetProfile, GetAnalystRatings, GetFinancialMetrics, GetInsiderTransactions, GetPriceTarget)
- `CompanyFundamentalsEntity` + `InsiderTransactionEntity` (database entities)
- `FundamentalsService`: 24h cache TTL, parallel data ophalen, bulk refresh voor watchlist
- `FundamentalsController`: GET /fundamentals/{symbol}, GET /fundamentals/{symbol}/insiders, POST /fundamentals/refresh-all (admin)
- `StockDetailPage.tsx`: volledige aandeel-detailpagina met 7 secties (waardering, winstgevendheid, groei, balans, dividend, analyst consensus bar, insider trading tabel)
- Route `/stock/:symbol` + watchlist symbolen zijn klikbare links
- Database tabellen: `company_fundamentals`, `insider_transactions`
- TypeScript types + API hooks

### EODHD Provider ✅
- **Branch `feature/eodhd-provider`** → gemerged (na rebase)
- `EodhdProvider.cs`: volledige implementatie van IMarketDataProvider + INewsProvider + IFundamentalsProvider
- Market data: EOD candles met adjusted close, symboollijsten per exchange
- Nieuws: met ingebouwd EODHD sentiment (polarity score)
- Fundamentals: profile, analyst ratings, financial metrics (via Highlights/Valuation/SharesStats filters), insider transactions, price targets
- Symbool mapping: `ASML.AS` → pass-through, `AAPL` → `AAPL.US`
- Rate limiting: ~90 calls/min met SemaphoreSlim
- Geregistreerd in ProviderManager factory
- EODHD provider_type bijgewerkt naar 'all' in seed data

---

## ⚠️ Bekend Issue: Algo Config verloren bij merges

Bij het mergen van `feature/fundamentals` en `feature/eodhd-provider` zijn de algo-config wijzigingen **verloren gegaan** door merge-overwrites. Dit moet hersteld worden in sessie 3.

### Wat ontbreekt op main
- `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` — hele bestand ontbreekt
- `AdminController.cs` — settings endpoints zijn teruggereset naar placeholders (lege `GetSettings` en `UpdateSettings`)
- `Program.cs` — `AlgoSettingsService` is niet geregistreerd als scoped service
- `frontend/src/pages/AdminSettingsPage.tsx` — onbekend of dit bestand nog aanwezig is
- `AppDbContext.cs` — `DbSet<AlgoSettingsEntity>` mogelijk ontbreekt

### Wat WEL nog aanwezig zou moeten zijn
- `AlgoSettingsEntity.cs` in Data/Entities
- `algo_settings` tabel in `database/init.sql` (met seed data)
- Frontend hooks in `useApi.ts` (useAlgoSettings, useUpdateAlgoSetting, useResetAlgoSettings)
- TypeScript types in `types/index.ts` (AlgoSettingsResponse etc.)

### Herstelactie voor sessie 3
Schrijf een prompt die:
1. `AlgoSettingsService.cs` herstelt (met de fixes uit prompt 02: static JsonSerializerOptions, weights validatie, thresholds validatie)
2. `AdminController.cs` herstelt (AlgoSettingsService injection, GET/PUT/POST settings endpoints, ArgumentException handling)
3. `Program.cs` bijwerkt (AddScoped<AlgoSettingsService>)
4. Verifieert dat AppDbContext de AlgoSettings DbSet heeft
5. Verifieert dat AdminSettingsPage.tsx op de frontend nog compleet is

---

## Open branches

| Branch | Status | Actie nodig |
|--------|--------|-------------|
| Geen | Alle branches opgeruimd | — |

---

## GitHub Issues (Roadmap)

| # | Titel | Status |
|---|-------|--------|
| [#3](https://github.com/mischaAxon/AxonStockAgent/issues/3) | Configureerbaar algoritme — gewichten instelbaar | ⚠️ Code verloren, moet hersteld |
| [#4](https://github.com/mischaAxon/AxonStockAgent/issues/4) | Nieuwsticker + Sector Sentiment Engine | ✅ Gemerged |
| [#5](https://github.com/mischaAxon/AxonStockAgent/issues/5) | Sector classificatie | ✅ Gemerged |
| [#6](https://github.com/mischaAxon/AxonStockAgent/issues/6) | Bedrijfsdata verzamelen | ✅ Gemerged |
| [#7](https://github.com/mischaAxon/AxonStockAgent/issues/7) | Auth + Rollen | ✅ Gemerged |
| [#8](https://github.com/mischaAxon/AxonStockAgent/issues/8) | Pluggable Provider Systeem | ✅ Gemerged |
| [#9](https://github.com/mischaAxon/AxonStockAgent/issues/9) | Roadmap — Master bouwvolgorde | Open (referentie) |

---

## Volgende stappen (sessie 3)

### Direct te doen
1. **Herstel algo-config code** — zie "Bekend Issue" hierboven
2. **ScreenerWorker scan-logica migreren** — het hart van de app: Worker die watchlist scant, technische indicatoren berekent, ML + sentiment + Claude combineert, signalen genereert. Gebruikt ProviderManager + AlgoSettings.

### Extra providers
3. **Alpha Vantage provider** (AI sentiment als second opinion)
4. Overige providers naar behoefte

### Productie
5. **CI/CD**: GitHub Actions → build → push naar Azure Container Registry
6. **Azure Container Apps** deployment
7. Custom domain + SSL
8. Monitoring + alerting

### Algoritme fine-tuning
9. Samen het algoritme bepalen — welke data telt voor wat
10. Per-sector gewichten (tech-aandelen anders dan utilities)
11. Backtesting framework

---

## Project Structuur (actueel)

```
AxonStockAgent/
├── docker-compose.yml / docker-compose.dev.yml
├── nginx/nginx.conf
├── database/init.sql
├── .env.example
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   │   ├── NewsTicker.tsx
│   │   │   ├── auth/ProtectedRoute.tsx
│   │   │   └── layout/Layout.tsx
│   │   ├── contexts/AuthContext.tsx
│   │   ├── hooks/useApi.ts
│   │   ├── services/api.ts + auth.ts
│   │   ├── pages/
│   │   │   ├── DashboardPage.tsx
│   │   │   ├── SignalsPage.tsx
│   │   │   ├── WatchlistPage.tsx (symbolen nu klikbaar → /stock/:symbol)
│   │   │   ├── StockDetailPage.tsx ← NIEUW (7-sectie aandeel detail)
│   │   │   ├── SectorsPage.tsx
│   │   │   ├── PortfolioPage.tsx
│   │   │   ├── NewsPage.tsx
│   │   │   ├── LoginPage.tsx
│   │   │   ├── RegisterPage.tsx
│   │   │   ├── AdminUsersPage.tsx
│   │   │   ├── AdminProvidersPage.tsx
│   │   │   └── AdminSettingsPage.tsx (⚠️ mogelijk incompleet)
│   │   └── types/index.ts (incl. CompanyFundamentals, InsiderTransaction, AlgoSettings types)
│   ├── Dockerfile / Dockerfile.dev
│   └── package.json
├── src/
│   ├── AxonStockAgent.sln
│   ├── AxonStockAgent.Core/
│   │   ├── Models/ (Candle, Signal, Portfolio, Config)
│   │   └── Interfaces/ (IMarketDataProvider, INewsProvider, IFundamentalsProvider ← uitgebreid)
│   ├── AxonStockAgent.Api/
│   │   ├── Controllers/ (Auth, Admin, Dashboard, Signals, Watchlist, Portfolio, Sectors, News, Fundamentals ← NIEUW)
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Entities/ (User, RefreshToken, Watchlist, Signal, Portfolio, Dividend, DataProvider, NewsArticle, SectorSentiment, AlgoSettings, CompanyFundamentals ← NIEUW, InsiderTransaction ← NIEUW)
│   │   ├── Services/ (JwtService, AuthService, ProviderManager, SectorService, NewsService, FundamentalsService ← NIEUW, AlgoSettingsService ← ⚠️ ONTBREEKT)
│   │   ├── Providers/ (FinnhubProvider ← uitgebreid, EodhdProvider ← NIEUW)
│   │   ├── BackgroundServices/ (NewsFetcherService)
│   │   ├── Dockerfile / Dockerfile.dev
│   │   └── Program.cs
│   └── AxonStockAgent.Worker/
│       ├── ScreenerWorker.cs (placeholder, scan logica nog te migreren)
│       └── Dockerfile / Dockerfile.dev
├── skills/ (frontend-react, backend, docker, communication)
└── docs/
    ├── ARCHITECTURE.md
    ├── AZURE_DEPLOY.md
    ├── CLAUDE_CODE_WORKFLOW.md
    ├── HANDOVER_SESSION_1.md
    ├── HANDOVER_SESSION_2.md ← dit bestand
    └── prompts/
        ├── 01-auth-backend.md
        ├── 02-algo-config-fixes.md
        ├── 03-fundamentals.md
        └── 04-eodhd-provider.md
```

## API Endpoints (actueel)

### Public
| Method | Endpoint | Beschrijving |
|--------|----------|-------------|
| POST | /api/v1/auth/register | Registratie (eerste user = admin) |
| POST | /api/v1/auth/login | Login → access + refresh token |
| POST | /api/v1/auth/refresh | Token vernieuwen |
| POST | /api/v1/auth/logout | Uitloggen |
| GET | /health | Health check |

### Protected (ingelogd)
| Method | Endpoint | Beschrijving |
|--------|----------|-------------|
| GET | /api/v1/auth/me | Huidige user |
| GET | /api/v1/dashboard | Dashboard aggregatie |
| GET/POST/DELETE | /api/v1/watchlist | Watchlist CRUD |
| GET | /api/v1/signals | Signalen (paginated + filters) |
| GET/POST/DELETE | /api/v1/portfolio | Portfolio CRUD |
| GET | /api/v1/sectors | Sector overzicht |
| GET | /api/v1/news/latest | Laatste nieuws |
| GET | /api/v1/news/sector-sentiment | Sentiment per sector |
| GET | /api/v1/news/trending | Trending symbolen |
| GET | /api/v1/fundamentals/{symbol} | Bedrijfsdata (24h cache) |
| GET | /api/v1/fundamentals/{symbol}/insiders | Insider transacties |

### Admin only
| Method | Endpoint | Beschrijving |
|--------|----------|-------------|
| GET/PUT | /api/v1/admin/users | Gebruikersbeheer |
| GET/PUT | /api/v1/admin/providers | Provider configuratie |
| POST | /api/v1/admin/providers/{name}/test | Test provider verbinding |
| GET/PUT | /api/v1/admin/settings | ⚠️ Placeholder (algo-config verloren) |
| POST | /api/v1/sectors/enrich | Verrijk alle watchlist sectoren |
| POST | /api/v1/fundamentals/refresh-all | Vernieuw alle fundamentals |

## Database Tabellen

watchlist, signals, portfolio, dividends, candle_cache, app_settings, audit_log, users, refresh_tokens, data_providers, news_articles, sector_sentiment, algo_settings (⚠️ tabel bestaat maar service ontbreekt), company_fundamentals ← NIEUW, insider_transactions ← NIEUW

## Data Providers (actueel)

| Provider | Type | Gratis | EU | Geïmplementeerd |
|----------|------|--------|----|-----------------|
| Finnhub | Alles | ✅ | Beperkt | ✅ Volledig (5 interface-methodes) |
| EODHD | Alles | €19.99/mnd | ✅ Uitstekend | ✅ Volledig (8 methodes) |
| Alpha Vantage | Alles | ✅ (25/dag) | Beperkt | 📋 Nog te bouwen |
| Twelve Data | Market data | $29/mnd | ✅ | 📋 Nog te bouwen |
| FMP | Fundamentals | $14/mnd | ✅ | 📋 Nog te bouwen |
| StockGeist | Nieuws/social | Freemium | ❌ | 📋 Nog te bouwen |
| NewsData.io | Nieuws | Freemium | ✅ | 📋 Nog te bouwen |
| Saxo OpenAPI | Market data | ✅ (klant) | ✅ | 📋 Nog te bouwen |

---

## Prompt voor nieuwe chat (sessie 3)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_2.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt.

⚠️ Eerste prioriteit: de algo-config code is verloren gegaan bij merges en moet hersteld worden. Zie "Bekend Issue" in de handover.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
