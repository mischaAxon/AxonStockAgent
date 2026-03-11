# AxonStockAgent — Claude Code Workflow

## Rolverdeling

### Claude Chat = Orchestrator / Architect
- Leest de repo (via GitHub tool) om de actuele staat te begrijpen
- Ontwerpt architectuur en schrijft gedetailleerde prompts
- Reviewt code na push door te verifiëren of het er goed uitziet
- Geeft de volgende stap / prompt
- Schrijft handover-documenten voor sessiecontinuïteit
- Kan direct commits pushen naar de repo (docs, handovers, prompts)

### Mischa + Claude Code = Builder
- Ontvangt prompts van Claude Chat en voert ze uit
- Bouwt features stap voor stap in de codebase
- Lost compilatie-issues zelfstandig op (bv. `$$"""` fix voor .NET 8)
- Commit direct naar main, pusht naar GitHub
- Rapporteert resultaat terug aan Claude Chat

## Workflow per feature

```
1. Claude Chat leest de laatste HANDOVER_SESSION_X.md
2. Mischa kiest wat er gebouwd moet worden
3. Claude Chat:
   a. Leest alle relevante bestanden uit de repo (GitHub tool)
   b. Ontwerpt de prompt met exacte code, bestandspaden, wijzigingen
   c. Levert de prompt als downloadbaar .md bestand
4. Mischa plakt de prompt in Claude Code
5. Claude Code bouwt alles en commit direct naar main
6. Mischa meldt "Done" + rapporteert eventuele fixes
7. Claude Chat verifieert via GitHub tool
8. Claude Chat pusht handover + prompt-referenties naar de repo
```

## Regels voor prompts

1. **Eén prompt = één feature of fix** — niet te groot, niet te klein
2. **Exacte bestandspaden** — geen ambiguïteit over waar iets moet
3. **Code blokken voor nieuwe bestanden** — Claude Code kan ze direct overnemen
4. **Diff-instructies voor wijzigingen** — "vervang X door Y" met beide code blokken
5. **Verificatiestappen** — `dotnet build`, `npx tsc --noEmit`, etc.
6. **Ontwerpkeuzes toelichten** — waarom, niet alleen wat

## Branch strategie

```
main ← alles gaat direct op main
```

- **Geen feature branches** — te veel overhead bij ons tempo
- Claude Code commit direct naar main
- Claude Chat kan docs direct naar main pushen
- Als iets misgaat: fix-commit erachteraan, niet reverteren

## Sessiecontinuïteit

### Handover documenten
- `docs/HANDOVER_SESSION_X.md` — compleet overzicht van wat er is gedaan
- Bevat: wijzigingen, open issues, volgende stappen, project structuur, sessie-prompt
- Elke nieuwe Claude Chat sessie begint met het lezen van de laatste handover

### Prompts archief
- `docs/prompts/XX-naam.md` — referentie naar elke uitgevoerde prompt
- Genummerd in volgorde van uitvoering
- Bevat minimaal: bestandslijst + korte beschrijving

### Session start prompt
Kopieer dit naar een nieuwe Claude Chat om verder te gaan:
```
Ik werk aan het project AxonStockAgent (https://github.com/mischaAxon/AxonStockAgent).
Lees eerst docs/HANDOVER_SESSION_X.md in de repo voor de volledige context.
We werken met een orchestrator-model: jij (Claude chat) ontwerpt en geeft mij
prompts, ik plak ze in Claude Code die het bouwt.
Vandaag wil ik verder met: [beschrijf wat je wilt doen]
```

## Tech stack referentie

| Laag | Tech |
|------|------|
| Backend API | ASP.NET Core 8, EF Core, PostgreSQL 16 |
| Worker | .NET 8 Worker Service |
| Frontend | React + Vite + TypeScript + Tailwind + TanStack Query |
| AI | Claude API (Anthropic) via directe HttpClient |
| Notificaties | Telegram Bot API |
| Infra | Docker Compose, Nginx reverse proxy, Redis |
| Target | Azure Container Apps |

## Taal

- Code: Engels (variabelen, comments, commit messages)
- UI labels: Engels
- Documentatie: Nederlands
- Beschrijvingen in seed data: Nederlands
- Communicatie met Claude: Nederlands
