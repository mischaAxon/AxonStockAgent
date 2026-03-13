# Prompt 33 — Fix Nieuws Sector Filter

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `frontend/src/pages/NewsPage.tsx` — sector filter combineert artikelen + sector_sentiment bronnen
- `src/.../Controllers/NewsController.cs` — max limit verhoogd naar 500
- `src/.../Services/NewsService.cs` — BackfillSectors() methode
- `src/.../Controllers/AdminController.cs` — POST news/backfill-sectors endpoint

## Beschrijving
Sector filter toonde alleen "Technology". Fix: sectoren uit twee bronnen (artikelen + sector_sentiment).
Backfill van 60 oude artikelen zonder sector. Limit verhoogd van 100 naar 500.
Resultaat: alle 9 sectoren zichtbaar in het filter.
