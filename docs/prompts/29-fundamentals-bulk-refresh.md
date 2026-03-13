# Prompt 29 — Fundamentals Bulk Refresh voor alle MarketSymbols

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/.../Services/FundamentalsService.cs` — RefreshAllMarketSymbolsFundamentals() methode
- `src/.../Controllers/AdminController.cs` — POST fundamentals/refresh-all + GET fundamentals/status
- `src/.../Worker/ScreenerWorker.cs` — Wekelijkse auto-refresh op zondagnacht

## Beschrijving
Bulk refresh van fundamentals voor alle actieve MarketSymbols (~98 symbolen, ~3-4 min door EODHD rate limiting).
Admin endpoint om handmatig te triggeren + status endpoint.
Worker voert automatisch elke zondagnacht een volledige refresh uit.
