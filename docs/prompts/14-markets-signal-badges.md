# Prompt 14 — Signaal Badges op Markets + Signals Cross-Symbol

**Sessie:** 8
**Status:** ✅ Geïmplementeerd

## Doel
Signaal badges (BUY/SELL/SQUEEZE + score%) tonen per symbool op het Markets scherm.

## Gewijzigde/nieuwe bestanden

### Backend (1 bestand)
| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Api/Controllers/SignalsController.cs` | **Gewijzigd** — nieuw `GET /v1/signals/latest-per-symbol?days=7` endpoint |

### Frontend (3 bestanden)
| Bestand | Actie |
|---------|-------|
| `frontend/src/types/index.ts` | **Gewijzigd** — `LatestSignalPerSymbol` interface |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** — `useLatestSignalsPerSymbol` hook (60s refresh) |
| `frontend/src/pages/MarketsPage.tsx` | **Gewijzigd** — `SignalBadge` component + signalMap integratie |
