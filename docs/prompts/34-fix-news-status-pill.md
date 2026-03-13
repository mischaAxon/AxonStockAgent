# Prompt 34 — Fix "Nieuws 0" Status Pill op Markets Pagina

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `frontend/src/pages/MarketsPage.tsx` — Nieuws pill gebruikt nu sector_sentiment articleCount

## Beschrijving
"Nieuws" status pill toonde altijd 0 (gebruikte sentimentMap.size van sentiment-changes endpoint).
Fix: telt nu articleCount op uit useSectorSentiment() data.
Resultaat: toont werkelijk artikelaantal (bv. "Nieuws 847").
