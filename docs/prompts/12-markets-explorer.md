# Prompt 12 — Markets Explorer: Beurzen per Land + Symbol Browser

**Sessie:** 8
**Status:** ✅ Geïmplementeerd

## Doel
Nieuw "Markets" scherm met alle symbolen gegroepeerd per land (met landvlag) → per beurs → symbolen met realtime prijs en mini-indicatoren. Klikken navigeert naar `/stock/:symbol`.

## Gewijzigde/nieuwe bestanden

### Backend (7 bestanden)
| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Core/Models/Quote.cs` | **Nieuw** — Quote model |
| `src/AxonStockAgent.Core/Interfaces/IMarketDataProvider.cs` | **Gewijzigd** — `GetQuote` methode |
| `src/AxonStockAgent.Api/Providers/FinnhubProvider.cs` | **Gewijzigd** — `GetQuote` via `/quote` |
| `src/AxonStockAgent.Api/Providers/EodhdProvider.cs` | **Gewijzigd** — `GetQuote` via `/real-time` |
| `src/AxonStockAgent.Api/Services/ProviderManager.cs` | **Gewijzigd** — `GetQuote` methode + using |
| `src/AxonStockAgent.Api/Controllers/ExchangesController.cs` | **Nieuw** — 3 endpoints |
| `src/AxonStockAgent.Api/Controllers/QuotesController.cs` | **Nieuw** — batch quotes |

### Frontend (6 bestanden)
| Bestand | Actie |
|---------|-------|
| `frontend/src/types/index.ts` | **Gewijzigd** — ExchangeInfo, MarketSymbol, Quote types |
| `frontend/src/hooks/useApi.ts` | **Gewijzigd** — 4 nieuwe hooks |
| `frontend/src/pages/MarketsPage.tsx` | **Nieuw** — Markets Explorer UI |
| `frontend/src/App.tsx` | **Gewijzigd** — `/markets` route |
| `frontend/src/components/layout/Layout.tsx` | **Gewijzigd** — Globe icon + Markets nav item |
