# Prompt 26 — Sentiment Change % metric

Nieuw endpoint + UI voor sentiment change percentage op tiles en detail pagina.

Bestanden:
- `src/.../Controllers/SignalsController.cs` (gewijzigd — sentiment-changes endpoint)
- `frontend/src/hooks/useApi.ts` (gewijzigd — useSentimentChanges)
- `frontend/src/types/index.ts` (gewijzigd — SentimentChange interface)
- `frontend/src/pages/MarketsPage.tsx` (gewijzigd — sentiment op tiles)
- `frontend/src/pages/StockDetailPage.tsx` (gewijzigd — sentiment metric card)
