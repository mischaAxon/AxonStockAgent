# Prompt 28 — Worker: Scan MarketSymbols + Pillar Scores + Scan Trigger

**Sessie:** 11
**Status:** Uitgevoerd ✅

## Bestanden
- `src/AxonStockAgent.Worker/ScreenerWorker.cs` — scan MarketSymbols, pillar scores, trigger polling
- `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` — GetStringAsync(), scan_source setting
- `src/AxonStockAgent.Api/Data/Entities/ScanTriggerEntity.cs` — Nieuw
- `src/AxonStockAgent.Api/Data/AppDbContext.cs` — DbSet ScanTriggers + mapping
- `src/AxonStockAgent.Api/Controllers/AdminController.cs` — scan/trigger + scan/status endpoints
- DB migratie: scan_triggers tabel + scan.scan_source algo setting

## Beschrijving
Worker scant nu alle MarketSymbols (index-leden) ipv alleen Watchlist.
FundamentalsScore en NewsScore worden gevuld bij signaal-opslag (pillar dots).
Scan trigger systeem: API maakt trigger aan, worker pollt en voert scan uit.
