# AxonStockAgent — Claude Code Workflow

## Hoe we werken

**Claude (chat)** = Orchestrator / Architect
- Ontwerpt de architectuur
- Schrijft gedetailleerde prompts voor Claude Code
- Reviewt resultaten en geeft volgende stap

**Mischa + Claude Code** = Builder
- Voert de prompts uit in de codebase
- Bouwt features stap voor stap
- Commit naar feature branches

## Regels voor Claude Code prompts

1. Elke prompt focust op één feature/taak
2. Prompt bevat altijd:
   - Welke branch te gebruiken
   - Welke bestanden aan te maken/wijzigen
   - Exacte interfaces en types
   - Wat te testen
3. Na elke prompt: commit + push
4. Na een feature: PR aanmaken

## Huidige status

Zie [Roadmap #9](https://github.com/mischaAxon/AxonStockAgent/issues/9)

### Fase 1: Auth + Providers
- [ ] #7 — Auth + Rollen (Admin/User JWT)
- [ ] #8 — Provider Plugin Systeem

### Fase 2: Data verrijking
- [ ] #5 — Sector Classificatie
- [ ] #6 — Bedrijfsdata
- [ ] #4 — Nieuwsticker + Sentiment

### Fase 3: Slim maken
- [ ] #3 — Configureerbaar Algoritme

## Branch strategie

```
main
├── feature/auth              ← #7
├── feature/providers         ← #8
├── feature/sectors           ← #5
├── feature/company-data      ← #6
├── feature/news-ticker       ← #4
└── feature/algo-config       ← #3
```
