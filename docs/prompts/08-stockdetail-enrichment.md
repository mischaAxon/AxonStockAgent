# Prompt 08 — StockDetailPage verrijken

**Sessie:** 4  
**Datum:** 11 maart 2026

## Bestanden

| Bestand | Actie |
|---------|-------|
| `frontend/src/pages/StockDetailPage.tsx` | Gewijzigd — 4 nieuwe secties toegevoegd |

## Beschrijving

Vier nieuwe secties boven de bestaande fundamentals:
1. Quick Stats Banner — laatste verdict + tijd, score, totaal signalen, gemiddelde score
2. Score Trend Chart — recharts LineChart met gekleurde dots, reference lines, dark tooltip
3. Signaalhistorie — tabel met 10 recentste signalen, link naar `/signals?symbol=X`
4. Recent Nieuws — 5 artikelen met sentiment dots

Geen backend wijzigingen.
