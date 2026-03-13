# AxonStockAgent — Handover Sessie 10

**Datum:** 13 maart 2026
**Repo:** https://github.com/mischaAxon/AxonStockAgent
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)
**Vorige handover:** `docs/HANDOVER_SESSION_9.md`

---

## Wat is er gedaan in sessie 10

Focus: **EdgeHound-geïnspireerde UX features — favorieten, vier-pijler scores, tabs, sentiment change**

Vier prompts geschreven (23 t/m 26), alle vier uitgevoerd. Gebaseerd op een diepgaande analyse van edgehound.com — hun symboolpagina's, trade ideas structuur, vier-pijler wegingsmodel (Fundamentals/Technical/Sentiment/News → Collective Oracle), en sentiment change metric.

### 1. Favoriet-ster op Markets tiles (Prompt 23) ✅

**Commit:** `18d4694`

**Probleem:** Gebruikers konden geen symbolen markeren als persoonlijke favoriet. Geen manier om snel terug te vinden wat je interessant vindt.

**Oplossing:**
- Nieuwe DB-tabel: `user_favorites` (user_id + symbol, uniek)
- `FavoriteEntity` — simpele koppeling user ↔ symbool
- `FavoritesController` — GET (lijst symbolen) + POST /{symbol} (toggle)
- Frontend: `useFavorites()` + `useToggleFavorite()` hooks
- Ster-icoon op elke Markets tile (verschijnt on hover, geel als favoriet)
- Nieuwe `★ FAV` filterknop in de Markets header bar naast ALL/BUY/SELL/SQZ
- `VerdictFilter` type uitgebreid met `'FAV'`

### 2. Vier-pijler gewogen score (Prompt 24) ✅

**Commit:** `ec6436a`

**Probleem:** Signalen toonden alleen een totaalscore — geen inzicht in welke factor (tech, fundamentals, sentiment, AI) het advies dreef.

**Oplossing:**
- Twee nieuwe nullable kolommen op `signals` tabel: `fundamentals_score`, `news_score`
- `SignalEntity` uitgebreid met `FundamentalsScore` en `NewsScore`
- `latest-per-symbol` endpoint retourneert nu ook de nieuwe velden
- Nieuw component: `PillarScoreBar` — vier gekleurde horizontale bars (Tech=blauw, Fund=groen, Sent=amber, AI=paars)
- Nieuw component: `PillarDots` — compacte variant (vier kleine dots) voor tiles
- StockDetailPage: vijfde metric card "Score Breakdown" met PillarScoreBar
- MarketsPage tiles: vier gekleurde dots naast de bestaande score badge
- Bestaande signalen zonder pillar data tonen graceful "—"

### 3. Symboolpagina tabs (Prompt 25) ✅

**Probleem:** StockDetailPage was één lange scrollende pagina — onoverzichtelijk bij veel data.

**Oplossing:**
- Vier tabs: **Signalen** | **Nieuws** | **Fundamentals** | **Profiel**
- Header + Live Price bar blijven altijd zichtbaar boven de tabs
- Tab "Signalen": Quick Stats Banner (5 cards) + Score Trend Chart + Signaalhistorie
- Tab "Nieuws": `NewsTab` sub-component met alle artikelen, sentimentscore per artikel, "toon meer" knop
- Tab "Fundamentals": Waardering + Winstgevendheid + Balans + Dividend + Omvang + Analisten + Insiders
- Tab "Profiel": `ProfileTab` met bedrijfsinfo grid + AI-analyse placeholder
- Conditional rendering (inactieve tabs renderen niet)

### 4. Sentiment Change % metric (Prompt 26) ✅

**Probleem:** Geen inzicht in hoe sentiment over tijd verandert — EdgeHound toont overal "Sentiment Change %".

**Oplossing:**
- Nieuw endpoint: `GET /api/v1/signals/sentiment-changes?days=7`
- Berekening: splitst de periode in twee helften, berekent gemiddeld sentiment per helft, retourneert het verschil
- Frontend: `SentimentChange` type + `useSentimentChanges()` hook
- MarketsPage tiles: `S:+3.2%` indicator (groen/rood, 8px font)
- StockDetailPage Signalen-tab: zesde metric card "Sentiment Δ" met change % en huidig sentiment
- Grid uitgebreid van 5 naar 6 kolommen

---

## Gewijzigde bestanden (sessie 10)

| Bestand | Actie | Prompt |
|---------|-------|--------|
| `src/.../Entities/FavoriteEntity.cs` | **Nieuw** | 23 |
| `src/.../Controllers/FavoritesController.cs` | **Nieuw** | 23 |
| `src/.../Data/AppDbContext.cs` | **Gewijzigd** — DbSet Favorites + mapping + SignalEntity kolommen | 23, 24 |
| `src/.../Entities/SignalEntity.cs` | **Gewijzigd** — FundamentalsScore, NewsScore | 24 |
| `src/.../Controllers/SignalsController.cs` | **Gewijzigd** — pillar velden in latest-per-symbol + sentiment-changes endpoint | 24, 26 |
| `frontend/src/types/index.ts` | **Gewijzigd** — Signal, LatestSignalPerSymbol uitgebreid + SentimentChange | 24, 26 |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** — useFavorites, useToggleFavorite, useSentimentChanges | 23, 26 |
| `frontend/src/components/PillarScoreBar.tsx` | **Nieuw** — PillarScoreBar + PillarDots | 24 |
| `frontend/src/pages/MarketsPage.tsx` | **Gewijzigd** — ster, FAV filter, PillarDots, sentiment indicator | 23, 24, 26 |
| `frontend/src/pages/StockDetailPage.tsx` | **Herschreven** — 4 tabs, PillarScoreBar card, NewsTab, ProfileTab, Sentiment Δ | 24, 25, 26 |
| DB migratie/SQL | `user_favorites` tabel, `fundamentals_score` + `news_score` kolommen | 23, 24 |

---

## Architectuur na sessie 10

### Nieuwe API endpoints

| Endpoint | Method | Beschrijving |
|----------|--------|--------------|
| `/api/v1/favorites` | GET | Lijst favoriete symbolen voor ingelogde user |
| `/api/v1/favorites/{symbol}` | POST | Toggle favoriet (voeg toe of verwijder) |
| `/api/v1/signals/sentiment-changes` | GET | Sentiment change per symbool over N dagen |

### Frontend pagina's (updated)

| Route | Pagina | Beschrijving |
|-------|--------|--------------|
| `/` | **Markets** | Trader grid — tiles met ster, pillar dots, sentiment change |
| `/stock/:symbol` | **Stock Detail** | 4 tabs: Signalen / Nieuws / Fundamentals / Profiel |
| `/signals` | Signalen | Alle signalen, filterable |
| `/sectors` | Sectoren | Sector overzicht |
| `/news` | Nieuws | Nieuwsartikelen + sentiment |
| `/admin/*` | Admin | Beurzen, Users, Providers, Settings |

### Nieuwe componenten

| Component | Locatie | Beschrijving |
|-----------|---------|-------------|
| `PillarScoreBar` | `components/PillarScoreBar.tsx` | Vier horizontale bars (Tech/Fund/Sent/AI) |
| `PillarDots` | `components/PillarScoreBar.tsx` | Compacte vier-dots variant voor tiles |
| `NewsTab` | Inline in `StockDetailPage.tsx` | Nieuws tab met sentiment scores + toon meer |
| `ProfileTab` | Inline in `StockDetailPage.tsx` | Bedrijfsprofiel + AI placeholder |

### Data flow: Markets scherm (updated)

```
MarketsPage
  ├─ useIndicesWithSymbols()       → GET /exchanges/indices-with-symbols
  ├─ useAllSymbols()               → GET /exchanges/all-symbols (fallback)
  ├─ useBatchQuotes(symbols)       → GET /quotes/batch (30s poll)
  ├─ useLatestSignalsPerSymbol     → GET /signals/latest-per-symbol (60s poll)
  ├─ useFavorites()                → GET /favorites
  ├─ useSentimentChanges(7)        → GET /signals/sentiment-changes (60s poll)
  └─ Per index: ExchangeColumn → Tile
       ├─ Ster-icoon (favoriet toggle)
       ├─ Verdict dot + score
       ├─ PillarDots (4 gekleurde stipjes)
       └─ S:±% (sentiment change)
```

### Data flow: StockDetailPage (updated)

```
StockDetailPage
  ├─ useWatchlist()                → Company info in header
  ├─ useBatchQuotes([symbol])      → Live price bar
  ├─ useSentimentChanges(7)        → Sentiment Δ card
  ├─ [Tab: Signalen]
  │   ├─ useSignals(symbol)        → Quick stats + chart + historie
  │   ├─ PillarScoreBar            → Score Breakdown card
  │   └─ Sentiment Δ card
  ├─ [Tab: Nieuws]
  │   └─ useNewsBySymbol(symbol)   → NewsTab met sentiment scores
  ├─ [Tab: Fundamentals]
  │   ├─ useFundamentals(symbol)   → Metrics cards
  │   └─ useInsiderTransactions()  → Insider tabel
  └─ [Tab: Profiel]
      └─ ProfileTab                → Bedrijfsinfo + AI placeholder
```

---

## EdgeHound inspiratie referentie

Deze sessie was gebaseerd op een analyse van edgehound.com. Relevante concepten die we hebben overgenomen:

| EdgeHound concept | Onze implementatie |
|-------------------|--------------------|
| Symbool → watchlist via app | Ster-icoon op tiles + FAV filter |
| Vier-pijler trade ideas (Fund/Tech/Sent/News) | PillarScoreBar + PillarDots |
| Symboolpagina tabs (Trade Ideas / Sentiment / Buzz Talk / General) | 4 tabs (Signalen / Nieuws / Fundamentals / Profiel) |
| Sentiment Change % als primaire metric | S:±% op tiles + Sentiment Δ card |
| Collective Oracle (multi-agent weging) | Toekomstig: per-pijler gewichten configureerbaar maken |
| Knowledge graphs (2e/3e orde relaties) | Toekomstig: gerelateerde symbolen + sector correlaties |
| Discovery Bot (AI chat per symbool) | Toekomstig: Claude chat in Profiel tab |

---

## Volgende stappen (sessie 11)

### Prioriteit 1: EdgeHound-geïnspireerde uitbreidingen
1. Prompt 15 uitvoeren — Info tooltips (staat nog klaar uit sessie 9)
2. Profiel tab vullen met Claude AI-gegenereerde bedrijfssamenvatting
3. Per-pijler gewichten configureerbaar maken in Admin → Algo Settings
4. ScreenerWorker: `fundamentals_score` en `news_score` daadwerkelijk vullen bij signaal-generatie

### Prioriteit 2: Markets UI verfijning
5. Sorteer-opties op Markets (change%, marktcap, sentiment, score)
6. Sector-filter op Markets
7. Batch quote-polling optimaliseren voor grote aantallen symbolen

### Prioriteit 3: CI/CD + Deployment
8. GitHub Actions workflow: build + test + Docker image push
9. Azure Container Apps configuratie
10. Custom domain + SSL

### Prioriteit 4: Data validatie
11. Worker laten draaien met echte data
12. Signal accuracy evalueren
13. Sentiment accuracy valideren tegen werkelijke koersbewegingen

---

## Prompt voor nieuwe chat (sessie 11)

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_10.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
