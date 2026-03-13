# Prompt 30 — Quote Coverage Fix + Orphan Cleanup

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/.../Controllers/AdminController.cs` — GET symbols/orphans + POST symbols/cleanup-orphans
- `src/.../Controllers/DiagnosticsController.cs` — GET quote-failures endpoint

## Beschrijving
Orphan symbolen identificeren en opschonen. 24 orphans gedeactiveerd (dubbele/foutieve tickers).
3 index-leden zonder EODHD-data gedeactiveerd (CTP.AS, FAGR.AS, WDP.AS — Belgisch/Tsjechisch).
71 actieve symbolen over, 66 met live koersdata (93% coverage).
"Overig AS" kolom verdwenen van het Markets scherm.
