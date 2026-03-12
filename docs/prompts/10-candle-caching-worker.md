# Prompt 10 — Candle-caching in Worker

**Status:** ✅ Voltooid  
**Sessie:** 7  
**Datum:** 12 maart 2026

## Samenvatting

In-memory candle caching via `IMemoryCache` om onnodige API-calls naar Finnhub/EODHD te voorkomen. TTL past zich automatisch aan op scan-modus.

## Wijzigingen

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Worker/Services/CandleCacheService.cs` | **Nieuw** — Singleton service met `IMemoryCache`, 24h TTL (EOD) / 5min TTL (realtime), cache key per symbol/resolution/count |
| `src/AxonStockAgent.Worker/Program.cs` | **Gewijzigd** — `AddMemoryCache` met SizeLimit 50.000, `CandleCacheService` als Singleton |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — `CandleCacheService` in constructor, `SetMode()` bij scan cycle start, candle fetches via cache |

## Ontwerpkeuzes

- **IMemoryCache i.p.v. Redis**: Worker is single-process, candle data is klein (~3.6 KB/symbool), cache hoeft geen herstart te overleven
- **Singleton lifetime**: cache-state moet bewaard blijven over scan cycles
- **SizeLimit 50.000**: vangnet tegen onbegrensde geheugengroei, ruim genoeg voor 500+ symbolen
- **Dynamische TTL**: EOD mode = 24h (data verandert niet na close), realtime = 5min (laatste bar kan veranderen)
