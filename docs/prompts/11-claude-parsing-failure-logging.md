# Prompt 11 — Claude parsing failure logging naar database

**Status:** ✅ Voltooid  
**Sessie:** 7  
**Datum:** 12 maart 2026

## Samenvatting

Elke Claude API interactie (succes én falen) wordt nu gelogd naar een dedicated `claude_api_logs` tabel. Admin endpoint geeft inzicht in success rate, faalredenen, latency en probleemtickets per symbool.

## Wijzigingen

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Api/Data/Entities/ClaudeApiLogEntity.cs` | **Nieuw** — Entity met symbol, status, HTTP code, direction, confidence, duration_ms, error_message, raw_response_snippet, model |
| `src/AxonStockAgent.Api/Data/AppDbContext.cs` | **Gewijzigd** — `ClaudeApiLogs` DbSet + model config met 3 indexes |
| `database/init.sql` | **Gewijzigd** — `claude_api_logs` tabel + indexes |
| `src/AxonStockAgent.Worker/Services/ClaudeAnalysisService.cs` | **Herschreven** — Stopwatch timing, `LogInteractionAsync` met eigen scope, nullable `scopeFactory` |
| `src/AxonStockAgent.Worker/ScreenerWorker.cs` | **Gewijzigd** — geeft `_scopeFactory` door aan ClaudeAnalysisService |
| `src/AxonStockAgent.Api/Controllers/DiagnosticsController.cs` | **Nieuw** — `GET /api/v1/diagnostics/claude/stats?days=30` (admin-only) |

## Ontwerpkeuzes

- **Dedicated tabel** i.p.v. generieke audit_log: specifieke velden voor filtering en aggregatie
- **IServiceScopeFactory** i.p.v. direct DbContext: ClaudeAnalysisService wordt handmatig geïnstantieerd, niet via DI
- **Nullable scopeFactory**: backward compatible — logging wordt overgeslagen als geen DI beschikbaar (bv. tests)
- **RawResponseSnippet**: key debugging info bij parse errors om prompt/parser te verbeteren
