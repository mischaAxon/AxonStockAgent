# AxonStockAgent — Handover Sessie 1

**Datum:** 10 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)

---

## Wat is AxonStockAgent?

Een AI-gedreven aandelenscreener die EU + US aandelen scant op technische signalen, deze verrijkt met ML.NET, Claude AI reasoning en nieuwssentiment, en via Telegram push-notificaties stuurt. Het project is uitgebouwd tot een volledige full-stack applicatie.

---

## Workflow

**Claude (chat) = Orchestrator** — ontwerpt architectuur, schrijft gedetailleerde prompts  
**Mischa + Claude Code = Builder** — voert prompts uit, bouwt features, commit naar branches  

Alle prompts staan in `docs/prompts/` in de repo. Claude Code leest het prompt-bestand en bouwt alles.

---

## Wat is gebouwd (sessie 1)

### Fase 0: Infrastructuur ✅
- **PR #1/#2** — Full-stack setup
- Docker Compose (6 services: nginx, frontend, api, worker, postgres, redis)
- .NET 8 solution: Core (shared) + Api (ASP.NET) + Worker (background)
- React frontend: Vite + TypeScript + Tailwind + TanStack Query
- PostgreSQL schema met watchlist, signals, portfolio, dividends, candle_cache, audit_log
- Nginx reverse proxy, multi-stage Dockerfiles, health checks
- Azure Container Apps deployment guide

### Fase 1: Auth + Providers ✅
- **PR #10** — JWT authenticatie (access + refresh tokens)
  - Admin/User rollen (eerste user = admin)
  - BCrypt hashing, JwtService, AuthService
  - AuthController: /register, /login, /refresh, /logout, /me
  - AdminController: users CRUD + settings placeholder
  - Frontend: LoginPage, RegisterPage, AuthContext, ProtectedRoute
  - Alle controllers beveiligd met [Authorize]

- **PR #11** — Provider Plugin Systeem
  - Core interfaces: IMarketDataProvider, INewsProvider, IFundamentalsProvider
  - FinnhubProvider implementeert alle 3 interfaces (rate limiting 55/min)
  - ProviderManager: laadt actieve providers uit DB, factory pattern
  - DataProviderEntity met 8 seeded providers (finnhub, eodhd, alpha_vantage, twelve_data, fmp, stockgeist, newsdata, saxo)
  - Admin API: GET/PUT providers, POST test verbinding
  - Frontend: AdminProvidersPage (kaarten, toggles, API key input, health check)

### Fase 2: Data Verrijking ✅
- **PR #12** — Sector Classificatie
  - SectorService: EnrichSymbol, EnrichAllWatchlist, GetSectorSummary
  - WatchlistItem uitgebreid: sector, industry, country, marketCap, logo, webUrl, sectorSource
  - SectorsController: GET sectoren, GET symbolen per sector, POST enrich (admin), PUT override (admin)
  - Auto-enrich bij toevoegen watchlist item
  - Frontend: SectorsPage + WatchlistPage met sector badges/filters

- **PR #13** — Nieuwsticker + Sector Sentiment
  - NewsService: FetchLatestNews (deduplicatie), CalculateSectorSentiment, GetLatestNews, GetTrending
  - NewsFetcherService: BackgroundService elke 60 sec (configureerbaar)
  - NewsController: /latest, /symbol/{symbol}, /sector-sentiment, /trending
  - Frontend: NewsTicker component (scrollende headlines bovenaan), NewsPage (heatmap + feed)

### Fase 3: Configureerbaar Algoritme 🔄 IN PROGRESS
- **Branch `feature/algo-config`** — gebouwd door Claude Code, nog niet gereviewed/gemerged
  - AlgoSettingsEntity + AlgoSettingsService
  - Typed config records: WeightsConfig, ThresholdsConfig, TechnicalWeightsConfig, ScanConfig, FeatureFlagsConfig
  - Admin API: GET/PUT settings, POST reset
  - Frontend: AdminSettingsPage met sliders, toggles, drempel configuratie

---

## Open branches

| Branch | Status | Actie nodig |
|--------|--------|-------------|
| `feature/algo-config` | Gebouwd | Review → merge naar main |
| `feature/full-stack-setup` | Gemerged | Kan verwijderd worden |
| `feature/auth` | Gemerged | Kan verwijderd worden |
| `feature/providers` | Gemerged | Kan verwijderd worden |
| `feature/sectors` | Gemerged | Kan verwijderd worden |
| `feature/news-ticker` | Gemerged | Kan verwijderd worden |

---

## GitHub Issues (Roadmap)

| # | Titel | Status |
|---|-------|--------|
| [#3](https://github.com/mischaAxon/AxonStockAgent/issues/3) | Configureerbaar algoritme — gewichten instelbaar | 🔄 Branch gebouwd |
| [#4](https://github.com/mischaAxon/AxonStockAgent/issues/4) | Nieuwsticker + Sector Sentiment Engine | ✅ Gemerged PR #13 |
| [#5](https://github.com/mischaAxon/AxonStockAgent/issues/5) | Sector classificatie | ✅ Gemerged PR #12 |
| [#6](https://github.com/mischaAxon/AxonStockAgent/issues/6) | Bedrijfsdata verzamelen (fundamentals, analyst ratings) | 📋 Open |
| [#7](https://github.com/mischaAxon/AxonStockAgent/issues/7) | Auth + Rollen (Admin/User JWT) | ✅ Gemerged PR #10 |
| [#8](https://github.com/mischaAxon/AxonStockAgent/issues/8) | Pluggable Provider Systeem | ✅ Gemerged PR #11 |
| [#9](https://github.com/mischaAxon/AxonStockAgent/issues/9) | 📋 Roadmap — Master bouwvolgorde | Open (referentie) |

---

## Volgende stappen (sessie 2)

### Direct te doen
1. **Review + merge `feature/algo-config`** naar main
2. **#6 — Bedrijfsdata verzamelen**: fundamentals, analyst ratings, insider trading opslaan per aandeel

### Extra providers bouwen
3. **EODHD provider** implementeren (IMarketDataProvider + INewsProvider + IFundamentalsProvider)
4. **Alpha Vantage provider** implementeren (AI sentiment als second opinion)
5. Overige providers naar behoefte

### Worker service migreren
6. **ScreenerWorker volledige scan logica** migreren van de originele SwingEdgeScreener.cs naar AxonStockAgent.Worker met gebruik van de nieuwe ProviderManager en AlgoSettings
7. Signalen opslaan in PostgreSQL via de API
8. Telegram notificaties via de bestaande NotificationService

### Productie
9. **CI/CD**: GitHub Actions → build → push naar Azure Container Registry
10. **Azure Container Apps** deployment
11. Custom domain + SSL
12. Monitoring + alerting

### Algoritme fine-tuning
13. Samen het algoritme bepalen — welke data telt voor wat
14. Per-sector gewichten (tech-aandelen anders dan utilities)
15. Backtesting framework

---

## Tech Stack (huidig)

| Laag | Technologie |
|------|-------------|
| Frontend | React 18 + Vite + TypeScript + Tailwind CSS + TanStack Query |
| API | ASP.NET Core 8 + Entity Framework Core + PostgreSQL |
| Worker | .NET 8 Worker Service |
| Database | PostgreSQL 16 |
| Cache | Redis 7 |
| AI | ML.NET FastTree + Claude API + Finnhub Sentiment |
| Infra | Docker Compose + Nginx reverse proxy |
| Deploy | Azure Container Apps (gepland) |

## Project Structuur

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
│   │   │   ├── WatchlistPage.tsx
│   │   │   ├── SectorsPage.tsx
│   │   │   ├── PortfolioPage.tsx
│   │   │   ├── NewsPage.tsx
│   │   │   ├── LoginPage.tsx
│   │   │   ├── RegisterPage.tsx
│   │   │   ├── AdminUsersPage.tsx
│   │   │   ├── AdminProvidersPage.tsx
│   │   │   └── AdminSettingsPage.tsx (op feature/algo-config)
│   │   └── types/index.ts
│   ├── Dockerfile / Dockerfile.dev
│   └── package.json
├── src/
│   ├── AxonStockAgent.sln
│   ├── AxonStockAgent.Core/
│   │   ├── Models/ (Candle, Signal, Portfolio, Config)
│   │   └── Interfaces/ (IMarketDataProvider, INewsProvider, IFundamentalsProvider, +legacy)
│   ├── AxonStockAgent.Api/
│   │   ├── Controllers/ (Auth, Admin, Dashboard, Signals, Watchlist, Portfolio, Sectors, News)
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Entities/ (User, RefreshToken, Watchlist, Signal, Portfolio, Dividend, DataProvider, NewsArticle, SectorSentiment)
│   │   ├── Services/ (JwtService, AuthService, ProviderManager, SectorService, NewsService)
│   │   ├── Providers/ (FinnhubProvider)
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
    └── prompts/01-auth-backend.md
```

## API Endpoints

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

### Admin only
| Method | Endpoint | Beschrijving |
|--------|----------|-------------|
| GET/PUT | /api/v1/admin/users | Gebruikersbeheer |
| GET/PUT | /api/v1/admin/providers | Provider configuratie |
| POST | /api/v1/admin/providers/{name}/test | Test provider verbinding |
| GET/PUT | /api/v1/admin/settings | Algoritme instellingen |
| POST | /api/v1/admin/settings/reset | Reset naar standaard |
| POST | /api/v1/sectors/enrich | Verrijk alle watchlist sectoren |

## Database Tabellen

watchlist, signals, portfolio, dividends, candle_cache, app_settings, audit_log, users, refresh_tokens, data_providers, news_articles, sector_sentiment, algo_settings (op feature/algo-config)

## Data Providers (ingebouwd)

| Provider | Type | Gratis | EU | Geïmplementeerd |
|----------|------|--------|----|-----------------|
| Finnhub | Alles | ✅ | Beperkt | ✅ Volledig |
| EODHD | Alles | €19.99/mnd | ✅ | 📋 Nog te bouwen |
| Alpha Vantage | Alles | ✅ (25/dag) | Beperkt | 📋 Nog te bouwen |
| Twelve Data | Market data | $29/mnd | ✅ | 📋 Nog te bouwen |
| FMP | Fundamentals | $14/mnd | ✅ | 📋 Nog te bouwen |
| StockGeist | Nieuws/social | Freemium | ❌ | 📋 Nog te bouwen |
| NewsData.io | Nieuws | Freemium | ✅ | 📋 Nog te bouwen |
| Saxo OpenAPI | Market data | ✅ (klant) | ✅ | 📋 Nog te bouwen |

---

## Prompt voor nieuwe chat

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_1.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt.

De `feature/algo-config` branch moet nog gereviewed en gemerged worden.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
