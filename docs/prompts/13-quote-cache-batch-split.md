# Prompt 13 — Quote Cache + Batch Splitting + StockDetail Realtime Prijs

**Sessie:** 8
**Status:** ✅ Geïmplementeerd

## Doel
Drie verbeteringen: server-side quote caching (30s TTL), frontend batch splitting voor >50 symbolen, en realtime prijs banner op StockDetailPage.

## Gewijzigde/nieuwe bestanden

### Backend (3 bestanden)
| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Api/Services/QuoteCacheService.cs` | **Nieuw** — 30s MemoryCache, batch met parallel miss-fill |
| `src/AxonStockAgent.Api/Controllers/QuotesController.cs` | **Gewijzigd** — route via cache + nieuw `GET /quotes/{symbol}` |
| `src/AxonStockAgent.Api/Program.cs` | **Gewijzigd** — AddMemoryCache + QuoteCacheService registratie |

### Frontend (2 bestanden)
| Bestand | Actie |
|---------|-------|
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** — useBatchQuotes met useQueries + chunks van 50 |
| `frontend/src/pages/StockDetailPage.tsx` | **Gewijzigd** — Live prijs banner met open/high/low/volume |
