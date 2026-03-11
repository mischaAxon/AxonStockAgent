# Prompt 05: Herstel Algo Config

Zie `docs/HANDOVER_SESSION_3.md` voor de volledige beschrijving.

Deze prompt herstelde alle algo-config code die verloren was gegaan bij merges in sessie 2.

## Bestanden aangemaakt/gewijzigd
1. `database/init.sql` — algo_settings tabel + 14 seed rows
2. `AlgoSettingsEntity.cs` — nieuw entity
3. `AppDbContext.cs` — DbSet + OnModelCreating
4. `AlgoSettingsService.cs` — service met validatie
5. `AdminController.cs` — 3 settings endpoints
6. `Program.cs` — AddScoped
7. `types/index.ts` — AlgoSetting types
8. `useApi.ts` — 3 hooks
9. `AdminSettingsPage.tsx` — volledige admin pagina
10. `App.tsx` — route toegevoegd

Volledige prompt was te groot voor opslag; zie het originele bestand dat via Claude chat is geleverd.
