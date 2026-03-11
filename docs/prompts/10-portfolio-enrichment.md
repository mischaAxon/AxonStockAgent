# Prompt 10 — PortfolioPage verrijken

**Sessie:** 4  
**Datum:** 11 maart 2026

## Bestanden

| Bestand | Actie |
|---------|-------|
| `frontend/src/hooks/useApi.ts` | Gewijzigd — useDeletePortfolio + useUpsertPortfolio notes |
| `frontend/src/types/index.ts` | Gewijzigd — PortfolioItem.updatedAt |
| `frontend/src/pages/PortfolioPage.tsx` | Vervangen — volledige upgrade |

## Beschrijving

Summary cards (waarde, posities, signalen), gestapelde allocatie bar met legenda (10 kleuren), verrijkte tabel (symbool-link, allocatie inline-bar, signaal badge + score, delete-knop), notes veld, empty state. Nieuwe `useDeletePortfolio` hook.
