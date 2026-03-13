# Prompt 31 — Fix Laatste Missende Quotes

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/.../Controllers/DiagnosticsController.cs` — quote-diagnose/{symbol} endpoint
- `src/.../Services/QuoteCacheService.cs` — SemaphoreSlim (max 5 concurrent), EOD cache 15min

## Beschrijving
Gedetailleerd diagnose-endpoint per symbool. Batch quote parallelisme beperkt tot max 5 gelijktijdige
EODHD calls (was onbeperkt). EOD fallback quotes worden 15 minuten gecacht ipv 30 seconden.

## Resultaat
- AD.AS, ADYEN.AS, APAM.AS: opgelost door rate limit fix (semaphore)
- VASTN.AS, TKWY.AS: geen EODHD data beschikbaar, gedeactiveerd
- 69 actieve symbolen, allemaal met koersdata
