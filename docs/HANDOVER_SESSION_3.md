# AxonStockAgent — Handover Sessie 3

**Datum:** 11 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_2.md`

---

## Wat is er gedaan in sessie 3

### Herstel Algo Config (verloren bij merges) ✅
- `AlgoSettingsEntity.cs` hersteld — entity met column-attributen
- `AlgoSettingsService.cs` hersteld — met alle review-fixes: static `JsonSerializerOptions`, weights-som validatie (±0.001 tolerantie), threshold cross-validatie (sell < buy < squeeze), `InvariantCulture` parsing
- `AdminController.cs` — 3 werkende settings endpoints: GET, PUT `/{id}`, POST `/reset`
- `AppDbContext.cs` — `DbSet<AlgoSettingsEntity>` + OnModelCreating config
- `Program.cs` — `AddScoped<AlgoSettingsService>()`
- `database/init.sql` — `algo_settings` tabel + 14 seed rows (5 weights, 3 thresholds, 3 scan, 3 notifications)
- `AdminSettingsPage.tsx` — volledige admin pagina met gewichtensom-indicator, toggles, inline errors, reset functie
- `types/index.ts` — `AlgoSetting` + `AlgoSettingsResponse` types
- `useApi.ts` — 3 hooks: `useAlgoSettings`, `useUpdateAlgoSetting`, `useResetAlgoSettings`
- `App.tsx` — route `/admin/settings` toegevoegd

### ScreenerWorker Scan-Logica ✅
- **Het hart van de app is nu gebouwd.** De Worker scant periodiek de watchlist en genereert signalen.

#### Nieuwe bestanden

| Bestand | Beschrijving |
|---------|-------------|
| `src/AxonStockAgent.Core/Analysis/IndicatorEngine.cs` | Technische analyse engine: EMA, SMA, RSI, MACD, ATR, Bollinger Band width. Normaliseert naar -1/+1 scores. |
| `src/AxonStockAgent.Worker/Services/ClaudeAnalysisService.cs` | Anthropic API via directe HttpClient. Stuurt technische data + headlines, parseert JSON assessment. Gebruikt `$$"""` raw strings. |
| `src/AxonStockAgent.Worker/Services/TelegramNotificationService.cs` | Telegram Bot API notificaties voor BUY/SELL/SQUEEZE signalen. |

#### Gewijzigde bestanden

| Bestand | Wijziging |
|---------|----------|
| `AxonStockAgent.Worker.csproj` | ProjectReference naar Api project toegevoegd |
| `Worker/Program.cs` | DbContext, ProviderManager, AlgoSettingsService, NewsService geregistreerd |
| `Worker/ScreenerWorker.cs` | Volledige scan-logica: watchlist → candles → TA → sentiment → Claude → gewogen score → signaal → DB → Telegram |
| `Worker/Dockerfile` | Api project mee gekopieerd in build |
| `Worker/Dockerfile.dev` | Idem |

#### Scan-flow in detail

```
Elke X minuten (uit AlgoSettings):
  ├── IsMarketHours()? (ma-vr 08:00-21:00 UTC)
  ├── Haal actieve symbolen uit watchlist
  ├── Lees gewichten/thresholds uit AlgoSettings (live, geen herstart nodig)
  ├── Per symbool:
  │   ├── Fetch candles via ProviderManager → IMarketDataProvider
  │   ├── Volume check (min_volume uit AlgoSettings)
  │   ├── IndicatorEngine.Analyze() → trend, momentum, volatiliteit, volume scores
  │   ├── Sentiment ophalen via INewsProvider
  │   ├── Claude AI analyse (graceful degradation als API key ontbreekt)
  │   ├── Gewogen eindscore: Σ(weight × normalized_score) / Σ(active_weights)
  │   ├── Verdict: SQUEEZE | BUY | SELL | HOLD (HOLD genereert geen signaal)
  │   ├── Opslaan in signals tabel
  │   └── Telegram notificatie (als geconfigureerd + enabled per type)
  └── Log: X/Y symbolen verwerkt, Z signalen gegenereerd
```

#### Ontwerpkeuzes
- **IServiceScopeFactory**: Worker is singleton, maar DbContext/ProviderManager/AlgoSettings zijn scoped → nieuwe scope per scan cycle
- **Graceful degradation**: Claude/sentiment/ML optioneel — als niet beschikbaar worden gewichten hernormaliseerd
- **Dynamische config**: scan interval, gewichten, thresholds live uit database (admin kan via UI aanpassen)
- **HOLD = geen signaal**: alleen BUY, SELL, SQUEEZE genereren records in de signals tabel
- **Rate limiting**: 500ms pauze tussen symbolen
- **`$$"""`**: .NET 8 raw string literal fix voor JSON templates met interpolatie

### Werkwijze vereenvoudigd
- **Geen feature branches meer** — alles gaat direct op main
- `docs/CLAUDE_CODE_WORKFLOW.md` bijgewerkt met de actuele werkwijze

---

## GitHub Issues (Roadmap)

| # | Titel | Status |
|---|-------|--------|
| [#3](https://github.com/mischaAxon/AxonStockAgent/issues/3) | Configureerbaar algoritme — gewichten instelbaar | ✅ Hersteld |
| [#4](https://github.com/mischaAxon/AxonStockAgent/issues/4) | Nieuwsticker + Sector Sentiment Engine | ✅ Gemerged |
| [#5](https://github.com/mischaAxon/AxonStockAgent/issues/5) | Sector classificatie | ✅ Gemerged |
| [#6](https://github.com/mischaAxon/AxonStockAgent/issues/6) | Bedrijfsdata verzamelen | ✅ Gemerged |
| [#7](https://github.com/mischaAxon/AxonStockAgent/issues/7) | Auth + Rollen | ✅ Gemerged |
| [#8](https://github.com/mischaAxon/AxonStockAgent/issues/8) | Pluggable Provider Systeem | ✅ Gemerged |
| [#9](https://github.com/mischaAxon/AxonStockAgent/issues/9) | Roadmap — Master bouwvolgorde | Open (referentie) |
| — | ScreenerWorker scan-logica | ✅ Gebouwd |

---

## Volgende stappen (sessie 4)

### Direct te doen
1. **End-to-end test** — `docker compose up`, verifieer dat Worker scant en signalen genereert
2. **Dashboard verrijken** — SignalsPage toont nu lege lijst, koppel aan live signalen

### Extra providers
3. **Alpha Vantage provider** — AI sentiment als second opinion
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
12. ML-model trainen op historische signalen

---

## Project Structuur (actueel)

```
AxonStockAgent/
├── docker-compose.yml / docker-compose.dev.yml
├── nginx/nginx.conf
├── database/init.sql (incl. algo_settings tabel + seed)
├── .env.example
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   │   ├── NewsTicker.tsx
│   │   │   ├── auth/ProtectedRoute.tsx
│   │   │   └── layout/Layout.tsx
│   │   ├── contexts/AuthContext.tsx
│   │   ├── hooks/useApi.ts (incl. algo settings hooks)
│   │   ├── services/api.ts + auth.ts
│   │   ├── pages/
│   │   │   ├── DashboardPage.tsx
│   │   │   ├── SignalsPage.tsx
│   │   │   ├── WatchlistPage.tsx
│   │   │   ├── StockDetailPage.tsx
│   │   │   ├── SectorsPage.tsx
│   │   │   ├── PortfolioPage.tsx
│   │   │   ├── NewsPage.tsx
│   │   │   ├── LoginPage.tsx
│   │   │   ├── RegisterPage.tsx
│   │   │   ├── AdminUsersPage.tsx
│   │   │   ├── AdminProvidersPage.tsx
│   │   │   └── AdminSettingsPage.tsx
│   │   └── types/index.ts (incl. AlgoSetting, AlgoSettingsResponse)
│   ├── Dockerfile / Dockerfile.dev
│   └── package.json
├── src/
│   ├── AxonStockAgent.sln
│   ├── AxonStockAgent.Core/
│   │   ├── Analysis/
│   │   │   └── IndicatorEngine.cs
│   │   ├── Models/ (Candle, Signal, Portfolio, Config)
│   │   └── Interfaces/ (IMarketDataProvider, INewsProvider, IFundamentalsProvider)
│   ├── AxonStockAgent.Api/
│   │   ├── Controllers/ (Auth, Admin, Dashboard, Signals, Watchlist, Portfolio, Sectors, News, Fundamentals)
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs (incl. AlgoSettings DbSet)
│   │   │   └── Entities/ (User, RefreshToken, Watchlist, Signal, Portfolio, Dividend, DataProvider, NewsArticle, SectorSentiment, AlgoSettings, CompanyFundamentals, InsiderTransaction)
│   │   ├── Services/ (JwtService, AuthService, ProviderManager, SectorService, NewsService, FundamentalsService, AlgoSettingsService)
│   │   ├── Providers/ (FinnhubProvider, EodhdProvider)
│   │   ├── BackgroundServices/ (NewsFetcherService)
│   │   ├── Dockerfile / Dockerfile.dev
│   │   └── Program.cs
│   └── AxonStockAgent.Worker/
│       ├── ScreenerWorker.cs
│       ├── Services/
│       │   ├── ClaudeAnalysisService.cs
│       │   └── TelegramNotificationService.cs
│       ├── Dockerfile / Dockerfile.dev
│       └── Program.cs
├── skills/ (frontend-react, backend, docker, communication)
└── docs/
    ├── ARCHITECTURE.md
    ├── AZURE_DEPLOY.md
    ├── CLAUDE_CODE_WORKFLOW.md
    ├── HANDOVER_SESSION_1.md
    ├── HANDOVER_SESSION_2.md
    ├── HANDOVER_SESSION_3.md ← dit bestand
    └── prompts/
        ├── 01-auth-backend.md
        ├── 03-fundamentals.md
        ├── 04-eodhd-provider.md
        ├── 05-restore-algo-config.md
        ├── 06-screener-worker.md
        └── README.md
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
| GET | /api/v1/admin/settings | Algo settings ophalen (gegroepeerd) |
| PUT | /api/v1/admin/settings/{id} | Setting updaten (met validatie) |
| POST | /api/v1/admin/settings/reset | Reset naar defaults |
| POST | /api/v1/sectors/enrich | Verrijk alle watchlist sectoren |
| POST | /api/v1/fundamentals/refresh-all | Vernieuw alle fundamentals |

## Database Tabellen

watchlist, signals, portfolio, dividends, candle_cache, app_settings, audit_log, users, refresh_tokens, data_providers, news_articles, sector_sentiment, algo_settings, company_fundamentals, insider_transactions

## Technische Indicatoren (IndicatorEngine)

| Indicator | Methode | Gebruik |
|-----------|---------|--------|
| EMA (20, 50) | Exponential Moving Average | Trend: crossover detectie |
| SMA (200) | Simple Moving Average | Trend: prijs vs langetermijn |
| RSI (14) | Relative Strength Index | Momentum: overbought/oversold |
| MACD (12, 26, 9) | Moving Average Convergence Divergence | Momentum: histogram richting |
| ATR (14) | Average True Range | Volatiliteit: als % van prijs |
| Bollinger Width (20, 2σ) | Bollinger Band breedte | Volatiliteit: squeeze detectie |
| Volume Ratio | Huidig vs 20-daags gemiddelde | Volume: bevestiging |

## Score Berekening

```
Eindscore = Σ(weight_i × normalized_score_i) / Σ(active_weights)

Normalisatie: alle scores → 0..1 bereik
- Tech score: (-1..+1) → (0..1) via (score + 1) / 2
- Sentiment: (-1..+1) → (0..1) via (score + 1) / 2
- Claude: 0..1 (SELL draait confidence: 1 - confidence)
- ML: 0..1 (placeholder, default 0.5)
- Fundamentals: 0..1 (placeholder, default 0.5)

Verdict:
- SQUEEZE: squeezeDetected && score >= squeeze_threshold
- BUY: score >= buy_threshold
- SELL: score <= sell_threshold
- HOLD: anders (geen signaal opgeslagen)
```

## Data Providers (actueel)

| Provider | Type | Gratis | EU | Geïmplementeerd |
|----------|------|--------|----|------------------|
| Finnhub | Alles | ✅ | Beperkt | ✅ Volledig |
| EODHD | Alles | €19.99/mnd | ✅ Uitstekend | ✅ Volledig |
| Alpha Vantage | Alles | ✅ (25/dag) | Beperkt | 📋 Nog te bouwen |
| Twelve Data | Market data | $29/mnd | ✅ | 📋 Nog te bouwen |
| FMP | Fundamentals | $14/mnd | ✅ | 📋 Nog te bouwen |
| StockGeist | Nieuws/social | Freemium | ❌ | 📋 Nog te bouwen |
| NewsData.io | Nieuws | Freemium | ✅ | 📋 Nog te bouwen |
| Saxo OpenAPI | Market data | ✅ (klant) | ✅ | 📋 Nog te bouwen |

---

## Prompt voor nieuwe chat (sessie 4)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_3.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
