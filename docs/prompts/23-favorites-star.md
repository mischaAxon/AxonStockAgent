# Prompt 23 — Favoriet-ster op Markets tiles

User favorites tabel + toggle endpoint + ster-icoon op trader grid tiles + FAV filter.

Bestanden:
- `src/.../Entities/FavoriteEntity.cs` (nieuw)
- `src/.../Controllers/FavoritesController.cs` (nieuw)
- `src/.../Data/AppDbContext.cs` (gewijzigd — DbSet + mapping)
- `frontend/src/hooks/useApi.ts` (gewijzigd — useFavorites, useToggleFavorite)
- `frontend/src/pages/MarketsPage.tsx` (gewijzigd — ster op tiles, FAV filter)
- DB migratie: `user_favorites`
