# AxonStockAgent — Multi-Agent Evolutieplan

**Geïnspireerd door:** ATLAS (General Intelligence Capital) — multi-agent trading framework met zelf-verbeterende prompts
**Datum:** 13 maart 2026

---

## Kernidee

ATLAS gebruikt 25 gespecialiseerde AI-agents in 4 lagen die elkaar voeden, met Darwinian weights (goed presterende agents krijgen meer invloed) en een autoresearch loop (slechtst presterende agent krijgt automatisch een nieuwe prompt). Wij kunnen dit stapsgewijs toepassen op AxonStockAgent.

## Huidige staat

Onze ScreenerWorker doet nu:
1. Technische indicatoren berekenen → `techScore`
2. Eén Claude API call met alle data → `claudeConfidence` + `claudeDirection` + `claudeReasoning`
3. Sentiment uit nieuws → `sentimentScore`
4. Gewogen combinatie → `finalScore` + `finalVerdict`

De vier-pijler UI (prompt 24) toont al Tech/Fund/Sent/AI bars, maar `fundamentalsScore` en `newsScore` worden nog niet gevuld.

## Evolutiepad (4 fasen)

### Fase 1: Pijlers vullen (sessie 11)

Doel: De bestaande vier-pijler UI werkend krijgen met echte data.

- ScreenerWorker vult `fundamentalsScore` op basis van CompanyFundamentals (P/E, groei, schuld, insider activity)
- ScreenerWorker vult `newsScore` op basis van recente nieuwssentiment per symbool
- Gewichten configureerbaar maken via AlgoSettings (bijv. tech_weight=0.3, fund_weight=0.2, sent_weight=0.2, ai_weight=0.3)
- `finalScore` = gewogen som van de vier pijlers

### Fase 2: Multi-pass Claude analyse (sessie 12-13)

Doel: Van één monolithische Claude call naar gespecialiseerde stappen.

Stap 1 — **Macro Agent**: Krijgt breed marktnieuws + indexdata → bepaalt regime (RISK_ON / RISK_OFF / NEUTRAL)
Stap 2 — **Sector Agent**: Krijgt macro regime + sector sentiment → rankt sectoren
Stap 3 — **Symbool Agent**: Krijgt sector context + technicals + fundamentals → BUY/SELL/HOLD + confidence
Stap 4 — **Risk Agent**: Reviewt alle output → past score aan op basis van risicofactoren

Elke stap is een aparte Claude API call met een gefocuste prompt. De output van stap N wordt input voor stap N+1.

Nieuwe DB-tabel: `agent_prompts` — slaat prompt-tekst per agent-type op, versioned.
Nieuw veld op SignalEntity: `macroRegime` (string), `sectorRank` (int nullable).

### Fase 3: Darwinian weights (sessie 14-15)

Doel: Automatisch leren welke pijler/agent het meest betrouwbaar is.

We tracken al outcome data (returnPct1d/5d/20d, outcomeCorrect). Per pijler berekenen we een rolling accuracy:
- Welke pijler voorspelde de juiste richting het vaakst?
- Pijlers met hogere accuracy krijgen meer gewicht in de finalScore

Nieuwe DB-tabel: `agent_weights` — per agent/pijler: gewicht (0.3–2.5), rolling_sharpe, updated_at.
Dagelijkse job: herbereken gewichten op basis van de laatste 30 dagen outcomes.
UI: Admin → Instellingen toont actuele gewichten + historische evolutie.

### Fase 4: Autoresearch loop (sessie 16+)

Doel: Het systeem verbetert zichzelf door prompt-varianten te testen.

1. Dagelijks: identificeer de pijler/agent met de laagste rolling Sharpe
2. Genereer een prompt-variatie (Claude schrijft een verbeterde versie van de eigen prompt)
3. Run de nieuwe prompt voor 5 handelsdagen
4. Meet: is de Sharpe verbeterd?
5. Ja → bewaar nieuwe prompt. Nee → revert naar de oude.

Nieuwe DB-tabel: `prompt_experiments` — prompt_id, variant_text, start_date, end_date, sharpe_before, sharpe_after, status (testing/accepted/reverted).

---

## Wat we NIET overnemen van ATLAS

- 25 agents — te complex, te duur qua API calls. Wij beginnen met 4 (macro/sector/symbool/risk)
- Superinvestor personas (Druckenmiller, Ackman etc.) — leuk maar gimmicky
- Live trade execution — wij zijn een signaal/advies platform, geen broker

## Geschatte API kosten per fase

| Fase | Claude calls/dag | Geschat/maand |
|------|-----------------|---------------|
| 1 (huidige staat) | 1 per symbool | ~$5-15 |
| 2 (multi-pass) | 4 per symbool | ~$20-60 |
| 3 (+ weights) | 4 per symbool + 1 daily | ~$25-65 |
| 4 (+ autoresearch) | 4 per symbool + 2 daily | ~$30-70 |

Afhankelijk van aantal getrackte symbolen (momenteel ~50-200).
