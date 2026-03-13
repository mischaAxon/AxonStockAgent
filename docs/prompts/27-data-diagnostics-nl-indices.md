# Prompt 27 — Data Diagnostics + Correcte NL Index Samenstelling

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/.../Controllers/DiagnosticsController.cs` — Herschreven, 3 nieuwe endpoints
- `src/.../Services/DutchIndexData.cs` — Nieuw, hardcoded NL index data
- `src/.../Controllers/AdminController.cs` — reload-nl endpoint
- `src/.../Providers/EodhdProvider.cs` — Verbeterde quote logging
- `src/.../Services/QuoteCacheService.cs` — Verbeterde batch logging

## Beschrijving
Diagnostics endpoints voor data-gezondheid, quote-testing, en batch-testing.
Hardcoded NL index samenstelling (AEX 30, AMX, AMS Next 20, AScX) ter vervanging van onbetrouwbare Claude AI import.
Verbeterde EODHD foutlogging met symboolformaat-details.
