# Prompt 28 — Worker: Scan MarketSymbols + Pillar Scores + Scan Trigger

**Sessie:** 11
**Status:** Klaar voor uitvoering

## Bestanden
- `src/AxonStockAgent.Worker/ScreenerWorker.cs` — scan MarketSymbols, pillar scores, trigger polling
- `src/AxonStockAgent.Api/Services/AlgoSettingsService.cs` — GetStringAsync()
- `src/AxonStockAgent.Api/Data/Entities/ScanTriggerEntity.cs` — Nieuw
- `src/AxonStockAgent.Api/Data/AppDbContext.cs` — DbSet ScanTriggers
- `src/AxonStockAgent.Api/Controllers/AdminController.cs` — scan/trigger + scan/status
- DB migratie: scan_triggers tabel

## Beschrijving
1. Worker scant MarketSymbols (alle index-leden) ipv alleen Watchlist
2. FundamentalsScore en NewsScore worden gevuld bij signaal-opslag → pillar dots
3. Scan trigger endpoint zodat je niet op 22:30 UTC hoeft te wachten
