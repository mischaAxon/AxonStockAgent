# Prompt 06: ScreenerWorker Scan-Logica

Zie `docs/HANDOVER_SESSION_3.md` voor de volledige beschrijving.

Deze prompt bouwde de volledige ScreenerWorker — het hart van de applicatie.

## Bestanden aangemaakt/gewijzigd
1. `AxonStockAgent.Worker.csproj` — Api projectref
2. `Worker/Program.cs` — services registreren
3. `Worker/ScreenerWorker.cs` — volledige scan-logica
4. `Core/Analysis/IndicatorEngine.cs` — technische analyse engine
5. `Worker/Services/ClaudeAnalysisService.cs` — Claude API integratie
6. `Worker/Services/TelegramNotificationService.cs` — Telegram notificaties
7. `Worker/Dockerfile` — updated voor Api project
8. `Worker/Dockerfile.dev` — idem

## Fix tijdens build
`$"""` raw string met `{{`/`}}` gaf CS9006 in .NET 8. Opgelost met `$$"""` waarbij interpolaties `{{expr}}` worden en JSON braces gewone `{`/`}` zijn.

Volledige prompt was te groot voor opslag; zie het originele bestand dat via Claude chat is geleverd.
