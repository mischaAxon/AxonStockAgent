# Prompt 09 — WatchlistPage verrijken

**Sessie:** 4  
**Datum:** 11 maart 2026

## Bestanden

| Bestand | Actie |
|---------|-------|
| `frontend/src/pages/WatchlistPage.tsx` | Vervangen — signalen, score bars, market cap, sortering |

## Beschrijving

Per card: laatste signaal (verdict badge + relatieve tijd + prijs), score bar, market cap. Stats balk bovenaan met verdeling. Sort dropdown: naam, sector, laatste signaal, market cap. Data via `useLatestSignals(50)` + signalMap.
