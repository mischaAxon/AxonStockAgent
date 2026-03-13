# Prompt 32 — Nieuws Pipeline Fix: MarketSymbols + Periodieke Fetch

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/.../Services/NewsService.cs` — MarketSymbols ipv Watchlist, rate limiting, geen dubbele sentiment calls
- `src/.../Worker/ScreenerWorker.cs` — FetchLatestNews + CalculateSectorSentiment na elke scan cycle
- `src/.../Worker/Program.cs` — NewsService DI registratie
- `src/.../Controllers/AdminController.cs` — POST news/fetch + GET news/status

## Beschrijving
Nieuws pipeline leest nu MarketSymbols (69 symbolen) ipv Watchlist (bijna leeg).
Worker haalt automatisch nieuws op na elke scan cycle.
Admin endpoints voor handmatig triggeren en status.

## Resultaat
- Van 7 symbolen met nieuws naar 69
- Van 1 sector (Technology) naar 9 sectoren
- Sector sentiment heatmap toont nu alle sectoren
- Nieuws wordt automatisch opgehaald na elke scan
