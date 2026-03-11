# AxonStockAgent — Instructies voor Claude Code

## Project overzicht
AxonStockAgent is een AI-aangedreven stock screener die automatisch technische analyse, sentiment, Claude AI-beoordeling en (toekomstig) ML combineert tot gewogen koop/verkoop-signalen met Telegram notificaties.

## Repo structuur
```
src/
  AxonStockAgent.Api/          # .NET 8 Web API (C#)
    Controllers/                # REST endpoints (Signals, Watchlist, Portfolio, Admin, etc.)
    Data/                       # EF Core DbContext + Entities
    Migrations/                 # EF Core migrations (PostgreSQL)
    Services/                   # AlgoSettings, ProviderManager
    Providers/                  # Market data & news provider implementaties
    BackgroundServices/         # Background jobs
  AxonStockAgent.Core/          # Gedeelde analyse-logica
    Analysis/                   # IndicatorEngine, technische indicatoren
    Interfaces/                 # IMarketDataProvider, INewsProvider
    Models/                     # ScreenerSignal, AiEnrichedSignal, ClaudeAssessment
  AxonStockAgent.Worker/        # Background worker (apart process)
    ScreenerWorker.cs           # Hoofdloop: scan watchlist → analyse → upsert signalen → notify
    Services/                   # ClaudeAnalysisService, TelegramNotificationService
  SwingEdgeScreener/            # Core screener algoritme
frontend/                       # React + TypeScript + Vite + Tailwind
  src/
    pages/                      # SignalsPage, DashboardPage, WatchlistPage, etc.
    components/                 # Shared components (VerdictBadge, ScoreBar)
    hooks/useApi.ts             # React Query hooks voor alle API calls
    types/index.ts              # TypeScript interfaces
    services/                   # API client configuratie
    contexts/                   # Auth context
    utils/                      # Formatters (relativeTime, etc.)
database/
  init.sql                      # Database schema initialisatie
scripts/
  cleanup-duplicate-signals.sql # Eenmalig: opschonen gestapelde signalen
  e2e-smoke-test.sh             # E2E test script
skills/                         # Claude skills/prompts
nginx/                          # Reverse proxy config
docker-compose.yml              # Productie Docker setup
docker-compose.dev.yml          # Development Docker setup
```

## Tech stack
- **Backend:** .NET 8, C#, Entity Framework Core, PostgreSQL
- **Frontend:** React 18, TypeScript, Vite, Tailwind CSS, React Query, Lucide icons
- **Worker:** .NET BackgroundService (apart proces)
- **Infra:** Docker Compose, Nginx reverse proxy
- **Externe APIs:** Claude API (analyse), Telegram Bot API (notificaties), diverse market data providers

## Belangrijke architectuur-beslissingen

### Signaal-flow
1. `ScreenerWorker` draait elke X minuten (instelbaar via `algo_settings.scan.scan_interval_minutes`)
2. Haalt actieve symbolen uit `watchlist` tabel
3. Per symbool: fetch candles → `IndicatorEngine.Analyze()` → sentiment → Claude AI
4. Berekent gewogen eindscore (gewichten uit `algo_settings.weights.*`)
5. Verdict: score ≤ 0.35 = SELL, ≥ 0.65 = BUY, squeeze detection = SQUEEZE, anders HOLD (geen signaal)
6. **Upsert-logica:** als er al een signaal bestaat voor hetzelfde symbool+verdict binnen `signal_dedup_minutes` (standaard 60 min), wordt het bestaande signaal geüpdatet i.p.v. een nieuw record aangemaakt. Dit voorkomt gestapelde duplicaten.
7. Telegram notificatie alleen bij **nieuwe** signalen, niet bij updates

### Score-systeem
- Alle subscores worden genormaliseerd naar 0–1 range
- `techScore` en `sentimentScore` komen binnen als -1 tot +1, genormaliseerd via `(score + 1) / 2`
- Claude confidence is al 0–1, maar wordt geïnverteerd bij SELL direction
- Gewichten configureerbaar via admin panel (`algo_settings` tabel)
- Frontend toont scores als 0–100% met uitleg in `ScoreExplainer` component

### Database
- PostgreSQL via EF Core
- Migrations in `src/AxonStockAgent.Api/Migrations/`
- Belangrijke tabellen: `signals`, `watchlist`, `portfolio`, `algo_settings`, `news_articles`, `company_fundamentals`
- Composiet index op `signals(Symbol, FinalVerdict, CreatedAt DESC)` voor snelle upsert lookups

### AlgoSettings
- Key-value store in `algo_settings` tabel met categorieën: `weights`, `thresholds`, `scan`, `notifications`
- Wijzigbaar via admin UI zonder herstart
- Service: `AlgoSettingsService` met `GetDecimalAsync()` en `GetBoolAsync()`

## Conventies

### Code
- C# backend: standaard .NET naming (PascalCase methods/properties)
- TypeScript frontend: camelCase, functionele React components met hooks
- Commit messages in het Engels, beschrijvend
- Nederlandse UI-teksten en comments
- Single responsibility: Workers scannen, API serveert, Core bevat logica

### Frontend
- Pagina's in `frontend/src/pages/`, componenten in `components/`
- Alle API types in `types/index.ts`
- API calls via React Query hooks in `hooks/useApi.ts`
- Tailwind voor styling, custom `axon-*` kleurenpalet
- Lucide React voor iconen

### Nieuwe features toevoegen
- Nieuw API endpoint → Controller in `Controllers/`, entity in `Data/Entities/`
- Nieuwe frontend pagina → component in `pages/`, route in `App.tsx`
- Nieuwe AlgoSetting → seed via SQL of via admin panel
- Database wijzigingen → EF Core migration (`dotnet ef migrations add <naam>`)

## Veelvoorkomende taken

```bash
# Lokaal draaien
docker-compose -f docker-compose.dev.yml up --build

# Database migration toevoegen
cd src/AxonStockAgent.Api
dotnet ef migrations add <NaamVanMigration>

# SQL script draaien (bv. cleanup)
docker exec -i axon-db psql -U axon -d axonstockagent < scripts/cleanup-duplicate-signals.sql

# Frontend dev server
cd frontend && npm run dev

# E2E smoke test
bash scripts/e2e-smoke-test.sh
```

## Bekende aandachtspunten
- Prijzen worden getoond met €-teken maar sommige bronnen leveren USD — valideer valuta per provider
- `signal_dedup_minutes` instelling bepaalt het dedup-window; na deze tijd wordt een nieuw signaal aangemaakt
- ML en Fundamentals modules zijn nog niet actief (N/A in UI) — worden niet meegewogen in score
- Market hours check: ma-vr 08:00–21:00 UTC
