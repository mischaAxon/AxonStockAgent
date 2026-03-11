# Prompt 07 — SignalsPage & Dashboard verrijken

**Sessie:** 4  
**Datum:** 11 maart 2026

## Bestanden

| Bestand | Actie |
|---------|-------|
| `frontend/src/pages/SignalsPage.tsx` | Vervangen — zoekbalk, periode filter, score bars, expandable detail rows, empty state |
| `frontend/src/pages/DashboardPage.tsx` | Vervangen — SQUEEZE card, scanner status, sector sentiment, trending |
| `frontend/src/utils/formatTime.ts` | Nieuw — `relativeTime()` helper (nl) |

## Beschrijving

SignalsPage: symbol zoekbalk (debounced 300ms), periode-dropdown, score bars met kleurcodering, expandable rows met volledige score breakdown (5 bars + status badges + Claude reasoning), mooie empty state, "Pagina X van Y".

DashboardPage: 5e stat card (SQUEEZE, amber), scanner status indicator (groene/grijze dot op 30 min threshold), sector sentiment grid via `useSectorSentiment()`, trending symbolen als pills, score bars + relatieve tijd bij recente signalen.
