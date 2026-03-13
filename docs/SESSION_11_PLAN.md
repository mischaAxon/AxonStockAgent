# AxonStockAgent — Sessie 11 Plan: Data Pipeline Werkend Krijgen

## Huidige staat (na Prompt 27)

### ✅ Wat werkt
- **Index-samenstelling** is gecorrigeerd (AEX 30, AMX Midcap, AMS Next 20, AScX)
- **Quotes** werken voor ~70% van de symbolen (69/98 live)
- **UI features** zijn compleet: sterren, pillar dots, sentiment change, tabs, filters
- **News ticker** bovenaan werkt

### ❌ Wat nog niet werkt
| Probleem | Oorzaak | Impact |
|----------|---------|--------|
| ~29 symbolen zonder koers ("—") | EODHD kent het tickerformaat niet | Lege tiles |
| Bijna geen signalen | Worker scant alleen Watchlist, niet MarketSymbols | Geen verdict dots |
| Geen fundamentals gevuld | Worden pas on-demand opgehaald | Lege Fundamentals tab |
| "Overig AS" vol orphans | Symbolen niet in een index | Rommelig |
| Sentiment change leeg | Geen signalen = geen history | Geen S:±% |

## Plan: 5 Prompts

| # | Prompt | Wat het fixt | Status |
|---|--------|-------------|--------|
| **28** | Worker scant MarketSymbols + pillar scores + scan trigger | Signalen voor alle symbolen | TODO |
| **29** | Fundamentals bulk refresh | Fundamentals tab gevuld | TODO |
| **30** | Quote coverage + orphan cleanup | Laatste ~29 missende symbolen | TODO |
| **31** | (gecombineerd met 28) | — | — |
| **32** | (gecombineerd met 28) | — | — |

## Uitvoeringsvolgorde

Prompt 28 → 29 → 30
