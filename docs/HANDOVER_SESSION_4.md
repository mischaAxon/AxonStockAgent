# AxonStockAgent — Handover Sessie 4

**Datum:** 11 maart 2026  
**Repo:** https://github.com/mischaAxon/AxonStockAgent  
**Eigenaar:** mischaAxon (Mischa de Jager, mischadejager@axonfactory.ai)  
**Vorige handover:** `docs/HANDOVER_SESSION_3.md`

---

## Wat is er gedaan in sessie 4

Focus: **alle frontend pagina's koppelen aan live data** — signalen, scores, charts en allocatie.

### 1. SignalsPage upgrade ✅
- Symbol zoekbalk met 300ms debounce
- Periode-dropdown (Vandaag / Deze week / Deze maand / Alles)
- Score bar per signaal (groen >60%, oranje 30-60%, rood <30%)
- **Expandable detail rows** — klik op een rij voor volledige breakdown:
  - 5 score bars: Technical, Sentiment, Claude AI, ML, Fundamentals
  - Status badges: trend, momentum, volatiliteit, volume
  - Volledige Claude reasoning (niet truncated)
- Mooie empty state met Zap icon
- Paginatie met "Pagina X van Y"
- **URL query param support** — `?symbol=AAPL` vult de zoekbalk en synct terug

### 2. DashboardPage upgrade ✅
- 5 stat cards (was 4): Watchlist, Portfolio, BUY, SELL, **SQUEEZE** (nieuw, amber)
- **Scanner status indicator** — groene/grijze dot rechtsboven (actief als laatste signaal <30 min)
- Recente signalen met score bars + relatieve tijd
- **Sector Sentiment widget** — grid met sentiment bars per sector, gesorteerd op score
- **Trending Symbolen widget** — pills met sentiment dots

### 3. DashboardController fix ✅
- `Id` toegevoegd aan de `recentSignals` Select-projectie
- Frontend gebruikte `signal.id` als React key — was missing in de API response

### 4. StockDetailPage verrijking ✅
- **Quick Stats Banner** — 4 cards: laatste verdict + tijd, score, totaal signalen, gemiddelde score
- **Score Trend Chart** — recharts LineChart met:
  - Gekleurde dots per verdict (groen/rood/amber)
  - Reference lines op buy (0.65) en sell (0.35) thresholds
  - Dark themed tooltip
  - Fallback: "Niet genoeg data" als <2 signalen
- **Signaalhistorie tabel** — 10 recentste signalen met score bars, Claude reasoning (60 chars)
  - "Bekijk alle signalen →" link naar `/signals?symbol=X`
- **Recent Nieuws** — 5 artikelen met sentiment dots, headline-links, bron, relatieve tijd
- Alle bestaande secties (fundamentals, insiders, analisten) behouden

### 5. SignalsPage URL sync ✅
- `useSearchParams` van react-router-dom
- Leest `?symbol=` bij mount → vult zoekbalk
- Synct search terug naar URL met `replace: true`
- StockDetailPage → "Bekijk alle signalen →" werkt nu end-to-end

### 6. WatchlistPage verrijking ✅
- **Signaal per card** — verdict badge + relatieve tijd + prijs + score bar
- **Market Cap** badge als beschikbaar
- **Sort dropdown** — 4 opties: naam, sector, laatste signaal, market cap
- **Stats balk** — "42 symbolen · 8 BUY · 3 SELL · 1 SQUEEZE"
- Data via `useLatestSignals(50)` + `useMemo` signalMap

### 7. PortfolioPage verrijking ✅
- **Verwijder-functie** — Trash2 icon per rij, `window.confirm()` bevestiging
- **`useDeletePortfolio` hook** toegevoegd in useApi.ts
- **Summary cards** — portefeuillewaarde, posities, posities met signaal
- **Allocatie bar** — gestapelde horizontale balk (10 kleuren) + legenda
- **Signaal per positie** — verdict badge + score + relatieve tijd
- **Symbool links** — elke ticker klikt door naar `/stock/{symbol}`
- **Notes veld** — formulier uitgebreid, `useUpsertPortfolio` hook gefixt
- **Empty state** met Briefcase icon
- `updatedAt` toegevoegd aan PortfolioItem type

### Gedeelde componenten & utilities
- `frontend/src/utils/formatTime.ts` — `relativeTime()` helper (Nederlands)
- `VerdictBadge`, `ScoreBar` als lokale componenten per pagina (nog niet geëxtraheerd naar shared)

---

## Gewijzigde bestanden (sessie 4)

| Bestand | Actie |
|---------|-------|
| `frontend/src/utils/formatTime.ts` | **Nieuw** |
| `frontend/src/pages/SignalsPage.tsx` | **Vervangen** (2x: upgrade + URL sync) |
| `frontend/src/pages/DashboardPage.tsx` | **Vervangen** |
| `frontend/src/pages/StockDetailPage.tsx` | **Vervangen** |
| `frontend/src/pages/WatchlistPage.tsx` | **Vervangen** |
| `frontend/src/pages/PortfolioPage.tsx` | **Vervangen** |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** (useDeletePortfolio + useUpsertPortfolio notes) |
| `frontend/src/types/index.ts` | **Gewijzigd** (PortfolioItem.updatedAt) |
| `src/AxonStockAgent.Api/Controllers/DashboardController.cs` | **Gewijzigd** (Id in recentSignals) |

---

## Bekende aandachtspunten

1. **VerdictBadge/ScoreBar duplicatie** — dezelfde componenten staan nu in SignalsPage, DashboardPage, StockDetailPage, WatchlistPage, PortfolioPage. Kandidaat voor extractie naar `frontend/src/components/shared/`.
2. **End-to-end test nog niet gedaan** — de Worker heeft nog niet gedraaid, dus alle pagina's tonen (correcte) lege staten. Prioriteit voor sessie 5.
3. **Periode filter client-side** — de SignalsPage filtert op tijd client-side (na fetch), wat niet werkt over pagina-grenzen heen. Overweeg backend `since` parameter.

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
| — | ScreenerWorker scan-logica | ✅ |
| — | Frontend pagina's koppelen aan live data | ✅ (sessie 4) |

---

## Volgende stappen (sessie 5)

### Prioriteit 1: End-to-end test
1. `docker compose up` — verifieer dat alles opstart
2. Registreer user, voeg watchlist items toe
3. Controleer dat Worker scant en signalen genereert
4. Verifieer dat alle pagina's data tonen

### Prioriteit 2: Shared componenten extraheren
5. VerdictBadge, ScoreBar, formatTime → `frontend/src/components/shared/`
6. Verwijder duplicatie uit alle pagina's

### Prioriteit 3: Backend verfijning
7. `since` query parameter op SignalsController (server-side tijdfilter)
8. Portfolio: huidige koers ophalen voor echte P&L berekening

### Prioriteit 4: Productie
9. CI/CD: GitHub Actions → build → push images
10. Azure Container Apps deployment
11. Custom domain + SSL

### Prioriteit 5: Algoritme
12. Backtesting framework
13. Per-sector gewichten
14. ML-model trainen

---

## Prompt voor nieuwe chat (sessie 5)

Kopieer dit naar een nieuwe Claude chat om verder te gaan:

```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).

Lees eerst docs/HANDOVER_SESSION_4.md en docs/CLAUDE_CODE_WORKFLOW.md in de repo voor de volledige context.

We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij prompts, ik plak ze in Claude Code die het bouwt. Alles gaat direct op main, geen feature branches.

Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```
