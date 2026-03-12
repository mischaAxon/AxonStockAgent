# Prompt 24 — Vier-pijler gewogen score

Expliciet tonen van Tech/Fundamentals/Sentiment/AI subscores per signaal.

Bestanden:
- `src/.../Entities/SignalEntity.cs` (gewijzigd — FundamentalsScore, NewsScore)
- `src/.../Controllers/SignalsController.cs` (gewijzigd — extra velden in latest-per-symbol)
- `src/.../Data/AppDbContext.cs` (gewijzigd — kolom mappings)
- `frontend/src/types/index.ts` (gewijzigd)
- `frontend/src/components/PillarScoreBar.tsx` (nieuw)
- `frontend/src/pages/StockDetailPage.tsx` (gewijzigd — PillarScoreBar card)
- `frontend/src/pages/MarketsPage.tsx` (gewijzigd — compacte pillar dots op tiles)
- DB migratie: fundamentals_score, news_score
