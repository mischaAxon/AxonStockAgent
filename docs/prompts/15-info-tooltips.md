# Prompt 15 — Info Tooltips: Uitleg bij alle indicatoren en metrics

## Doel

Voeg een herbruikbaar `InfoTooltip` component toe dat een ⓘ-icoon toont met uitleg bij hover (desktop) en bij klik/tap (mobiel). Pas dit toe op **alle** metrics, indicatoren, scores en percentages in de hele frontend.

## Verificatie achteraf

```bash
cd frontend
npx tsc --noEmit
npm run build
```

---

## Stap 1: Maak `frontend/src/components/shared/InfoTooltip.tsx`

Maak een nieuw bestand `frontend/src/components/shared/InfoTooltip.tsx`:

```tsx
import { useState, useRef, useEffect } from 'react';
import { Info } from 'lucide-react';

interface InfoTooltipProps {
  text: string;
  size?: number;
}

export default function InfoTooltip({ text, size = 13 }: InfoTooltipProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Close on outside click (mobile)
  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  return (
    <div ref={ref} className="relative inline-flex items-center">
      <button
        type="button"
        onClick={() => setOpen(prev => !prev)}
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        className="text-gray-600 hover:text-gray-400 transition-colors focus:outline-none ml-1"
        aria-label="Meer informatie"
      >
        <Info size={size} />
      </button>
      {open && (
        <div className="absolute z-50 bottom-full left-1/2 -translate-x-1/2 mb-2 w-64 px-3 py-2 rounded-lg bg-gray-800 border border-gray-700 shadow-xl text-xs text-gray-300 leading-relaxed pointer-events-none">
          {text}
          <div className="absolute top-full left-1/2 -translate-x-1/2 -mt-px w-0 h-0 border-l-[6px] border-r-[6px] border-t-[6px] border-l-transparent border-r-transparent border-t-gray-700" />
        </div>
      )}
    </div>
  );
}
```

## Stap 2: Exporteer in `frontend/src/components/shared/index.ts`

Voeg toe aan het einde van `frontend/src/components/shared/index.ts`:

```ts
export { default as InfoTooltip } from './InfoTooltip';
```

---

## Stap 3: Definieer alle tooltip-teksten

Maak een nieuw bestand `frontend/src/utils/tooltipTexts.ts`:

```ts
// ─── Fundamentals: Waardering ────────────────────────────────────────────────
export const TOOLTIPS = {
  // Valuation
  peRatio: 'Price-to-Earnings ratio. Aandeelprijs gedeeld door winst per aandeel. Lager dan 15 = goedkoop (groen), 15–25 = redelijk (geel), boven 25 = duur (rood).',
  forwardPe: 'Forward P/E. Zoals P/E, maar gebaseerd op verwachte toekomstige winst. Lager is over het algemeen beter.',
  pbRatio: 'Price-to-Book ratio. Aandeelprijs gedeeld door boekwaarde per aandeel. Onder 3 is doorgaans aantrekkelijk.',
  psRatio: 'Price-to-Sales ratio. Marktkapitalisatie gedeeld door omzet. Onder 2 is doorgaans aantrekkelijk.',
  evToEbitda: 'Enterprise Value / EBITDA. Totale bedrijfswaarde gedeeld door operationele kasstroom. Onder 15 is doorgaans aantrekkelijk.',

  // Profitability
  profitMargin: 'Nettomarge. Percentage van de omzet dat overblijft als nettowinst. Hoger is beter.',
  operatingMargin: 'Operationele marge. Percentage van de omzet dat overblijft na operationele kosten, vóór belasting en rente.',
  returnOnEquity: 'Return on Equity (ROE). Hoeveel winst het bedrijf maakt per euro eigen vermogen. Boven 15% is sterk.',
  returnOnAssets: 'Return on Assets (ROA). Hoeveel winst het bedrijf genereert per euro totaal vermogen.',
  revenueGrowthYoy: 'Omzetgroei jaar-op-jaar. Procentuele stijging/daling van de omzet t.o.v. vorig jaar.',
  earningsGrowthYoy: 'Winstgroei jaar-op-jaar. Procentuele stijging/daling van de nettowinst t.o.v. vorig jaar.',

  // Balance sheet
  debtToEquity: 'Schuld/Eigen vermogen. Totale schuld gedeeld door eigen vermogen. Onder 1 = conservatief (groen), 1–2 = matig (geel), boven 2 = hoge hefboom (rood).',
  currentRatio: 'Current Ratio. Vlottende activa gedeeld door kortlopende schulden. Boven 1.5 = gezond (groen), 1–1.5 = acceptabel (geel), onder 1 = risicovol (rood).',
  quickRatio: 'Quick Ratio. Zoals Current Ratio, maar zonder voorraden. Boven 1.5 = gezond, onder 1 = risicovol.',

  // Dividends
  dividendYield: 'Dividendrendement. Jaarlijks dividend als percentage van de aandeelprijs.',
  payoutRatio: 'Uitkeringsratio. Percentage van de winst dat als dividend wordt uitgekeerd. Onder 60% is duurzaam (groen), 60–80% = hoog (geel), boven 80% = risicovol (rood).',

  // Size
  marketCap: 'Marktkapitalisatie. Totale beurswaarde: aandeelprijs × aantal uitstaande aandelen.',
  revenue: 'Omzet (Trailing Twelve Months). Totale omzet over de afgelopen 12 maanden.',
  netIncome: 'Nettoresultaat. Winst (of verlies) na alle kosten, belastingen en rente.',

  // Analyst
  analystConsensus: 'Verdeling van analistenaanbevelingen: Strong Buy, Buy, Hold, Sell, Strong Sell. Gebaseerd op recente broker-rapporten.',
  targetPrice: 'Koersdoel van analisten. Het verwachte prijsniveau over 12 maanden, op basis van gemiddelden van meerdere analisten.',

  // ─── Signal & Score ──────────────────────────────────────────────────────
  finalScore: 'Gecombineerde score (0–100%). Gewogen gemiddelde van alle deelscores (technisch, sentiment, AI, ML, fundamentals). Boven 60% = BUY zone (groen), 30–60% = neutraal (geel), onder 30% = SELL zone (rood).',
  avgScore: 'Gemiddelde score over alle signalen voor dit symbool. Geeft een beeld van de historische signaalsterkte.',
  totalSignals: 'Totaal aantal signalen dat de scanner voor dit symbool heeft gegenereerd.',
  verdict: 'Signaaltype. BUY = koopsignaal, SELL = verkoopsignaal, SQUEEZE = verwachte sterke koersbeweging (hoge volatiliteit, lage Bollinger Band-breedte).',

  // Score breakdown
  techScore: 'Technische score. Gebaseerd op technische indicatoren: RSI, MACD, Bollinger Bands, voortschrijdende gemiddelden en volume-analyse. Genormaliseerd naar 0–100%.',
  sentimentScore: 'Sentimentscore. Afgeleid van recente nieuwsartikelen en hun sentimentanalyse. 0% = zeer negatief, 50% = neutraal, 100% = zeer positief.',
  claudeAI: 'Claude AI-score. Anthropic\'s Claude analyseert alle beschikbare data en geeft een eigen beoordeling met confidence-percentage en richting (BUY/SELL).',
  mlScore: 'Machine Learning score. Voorspelling van een ML-model getraind op historische signaaldata. Geeft de waarschijnlijkheid van een succesvolle trade.',
  fundamentalsScore: 'Fundamentals score. Beoordeling op basis van financiële kengetallen (P/E, groei, marges, schuld). Nog in ontwikkeling.',

  // Status indicators
  trendStatus: 'Trendstatus. Richting van het voortschrijdend gemiddelde: UPTREND, DOWNTREND of SIDEWAYS.',
  momentumStatus: 'Momentum. Kracht van de koersbeweging op basis van RSI en MACD: STRONG, MODERATE of WEAK.',
  volatilityStatus: 'Volatiliteit. Mate van koersschommelingen op basis van Bollinger Bands: HIGH, NORMAL of LOW.',
  volumeStatus: 'Volume. Handelsvolume t.o.v. het gemiddelde: ABOVE AVERAGE, NORMAL of BELOW AVERAGE.',

  // ─── Score Trend Chart ───────────────────────────────────────────────────
  scoreTrend: 'Score Trend. Laat de ontwikkeling van de gecombineerde score zien over tijd. Stippellijn boven (65%) = BUY-drempel. Stippellijn onder (35%) = SELL-drempel.',

  // ─── Live Price ──────────────────────────────────────────────────────────
  livePrice: 'Huidige koers van het aandeel, opgehaald via de actieve data-provider. Wordt elke 30 seconden ververst.',
  changePercent: 'Koersverandering vandaag als percentage t.o.v. de slotkoers van gisteren.',
  open: 'Openingskoers. De prijs bij de eerste transactie van de handelsdag.',
  high: 'Dagkoers hoog. De hoogste prijs bereikt tijdens de huidige handelsdag.',
  low: 'Dagkoers laag. De laagste prijs bereikt tijdens de huidige handelsdag.',
  prevClose: 'Vorige slotkoers. De slotkoers van de vorige handelsdag.',
  volume: 'Handelsvolume. Aantal verhandelde aandelen vandaag.',

  // ─── Dashboard ───────────────────────────────────────────────────────────
  watchlistCount: 'Aantal symbolen op je watchlist die actief worden gemonitord door de scanner.',
  portfolioPositions: 'Aantal unieke posities in je portfolio.',
  weekBuys: 'Aantal BUY-signalen gegenereerd in de afgelopen 7 dagen.',
  weekSells: 'Aantal SELL-signalen gegenereerd in de afgelopen 7 dagen.',
  weekSqueezes: 'Aantal SQUEEZE-signalen in de afgelopen 7 dagen. Een squeeze duidt op verwachte sterke koersbeweging.',
  sectorSentiment: 'Gemiddeld sentiment per sector. Berekend uit recente nieuwsartikelen. Schaal: -1 (zeer negatief) tot +1 (zeer positief). Boven +0.1 = positief (groen), onder -0.1 = negatief (rood).',
  trending: 'Symbolen die het vaakst in het nieuws voorkomen. De gekleurde stip geeft het gemiddelde sentiment aan.',

  // ─── Markets ─────────────────────────────────────────────────────────────
  signalBadge: 'Meest recente signaal voor dit symbool (afgelopen 7 dagen). Toont het verdict (BUY/SELL/SQUEEZE) en de gecombineerde score.',

  // ─── Watchlist ───────────────────────────────────────────────────────────
  watchlistMarketCap: 'Marktkapitalisatie. Totale beurswaarde van het bedrijf.',

  // ─── Portfolio ───────────────────────────────────────────────────────────
  portfolioValue: 'Geschatte portefeuillewaarde op basis van je gemiddelde aankoopprijs × aantal aandelen. Geen live koersen.',
  allocation: 'Allocatie. Procentueel aandeel van elke positie in je totale portefeuille, gebaseerd op aankoopwaarde.',
} as const;
```

---

## Stap 4: Pas `StockDetailPage.tsx` aan

Import bovenaan toevoegen (bij de bestaande imports van `../components/shared`):

```tsx
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
```

En importeer tooltips:

```tsx
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 4a. MetricCard component aanpassen

Vervang de bestaande `MetricCard` functie door:

```tsx
function MetricCard({ label, value, color, tooltip }: { label: string; value: string; color: string; tooltip?: string }) {
  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
      <p className="text-xs text-gray-500 mb-1 flex items-center">
        {label}
        {tooltip && <InfoTooltip text={tooltip} />}
      </p>
      <p className={`text-xl font-bold ${color}`}>{value}</p>
    </div>
  );
}
```

### 4b. Fundamentals — Waardering sectie

Vervang de grid met MetricCards in de "Waardering" section door:

```tsx
<MetricCard label="P/E Ratio"    value={fmt(fund.peRatio)}    color={peColor(fund.peRatio)}    tooltip={TOOLTIPS.peRatio} />
<MetricCard label="Forward P/E"  value={fmt(fund.forwardPe)}  color={peColor(fund.forwardPe)}  tooltip={TOOLTIPS.forwardPe} />
<MetricCard label="P/B Ratio"    value={fmt(fund.pbRatio)}    color={fund.pbRatio != null && fund.pbRatio < 3 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.pbRatio} />
<MetricCard label="P/S Ratio"    value={fmt(fund.psRatio)}    color={fund.psRatio != null && fund.psRatio < 2 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.psRatio} />
<MetricCard label="EV/EBITDA"    value={fmt(fund.evToEbitda)} color={fund.evToEbitda != null && fund.evToEbitda < 15 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.evToEbitda} />
```

### 4c. Winstgevendheid & Groei sectie

```tsx
<MetricCard label="Nettomarge"         value={fmtPct(fund.profitMargin)}      color={pctColor(fund.profitMargin)}      tooltip={TOOLTIPS.profitMargin} />
<MetricCard label="Operationele marge" value={fmtPct(fund.operatingMargin)}   color={pctColor(fund.operatingMargin)}   tooltip={TOOLTIPS.operatingMargin} />
<MetricCard label="ROE"                value={fmtPct(fund.returnOnEquity)}    color={pctColor(fund.returnOnEquity)}    tooltip={TOOLTIPS.returnOnEquity} />
<MetricCard label="ROA"                value={fmtPct(fund.returnOnAssets)}    color={pctColor(fund.returnOnAssets)}    tooltip={TOOLTIPS.returnOnAssets} />
<MetricCard label="Omzetgroei (YoY)"   value={fmtPct(fund.revenueGrowthYoy)}  color={pctColor(fund.revenueGrowthYoy)}  tooltip={TOOLTIPS.revenueGrowthYoy} />
<MetricCard label="Winstgroei (YoY)"   value={fmtPct(fund.earningsGrowthYoy)} color={pctColor(fund.earningsGrowthYoy)} tooltip={TOOLTIPS.earningsGrowthYoy} />
```

### 4d. Balans sectie

```tsx
<MetricCard label="Schuld/Eigen vermogen" value={fmt(fund.debtToEquity)} color={deColor(fund.debtToEquity)} tooltip={TOOLTIPS.debtToEquity} />
<MetricCard label="Current Ratio"         value={fmt(fund.currentRatio)} color={ratioColor(fund.currentRatio)} tooltip={TOOLTIPS.currentRatio} />
<MetricCard label="Quick Ratio"           value={fmt(fund.quickRatio)}   color={ratioColor(fund.quickRatio)} tooltip={TOOLTIPS.quickRatio} />
```

### 4e. Dividend sectie

```tsx
<MetricCard label="Dividendrendement" value={fmtPct(fund.dividendYield)} color="text-white" tooltip={TOOLTIPS.dividendYield} />
<MetricCard label="Uitkeringsratio"   value={fmtPct(fund.payoutRatio)}   color={payoutColor(fund.payoutRatio)} tooltip={TOOLTIPS.payoutRatio} />
```

### 4f. Omvang sectie

```tsx
<MetricCard label="Marktkapitalisatie" value={fmtLarge(fund.marketCap) !== '—' ? '$' + fmtLarge(fund.marketCap) : '—'} color="text-white" tooltip={TOOLTIPS.marketCap} />
<MetricCard label="Omzet (TTM)"        value={fund.revenue   != null ? '$' + fmtLarge(fund.revenue)   : '—'} color="text-white" tooltip={TOOLTIPS.revenue} />
<MetricCard label="Nettoresultaat"     value={fund.netIncome != null ? '$' + fmtLarge(fund.netIncome) : '—'} color={pctColor(fund.netIncome)} tooltip={TOOLTIPS.netIncome} />
```

### 4g. Quick Stats Banner — Score cards

Zoek de 4 quick stat cards (Laatste Signaal, Score, Totaal Signalen, Gemiddelde Score) en voeg tooltips toe aan de labels. Vervang elke `<p className="text-xs text-gray-500 mb-1.5">Label</p>` door `<p className="text-xs text-gray-500 mb-1.5 flex items-center">Label<InfoTooltip text={TOOLTIPS.xxx} /></p>`.

Specifiek:
- "Laatste Signaal" → `TOOLTIPS.verdict`
- "Score" → `TOOLTIPS.finalScore`
- "Totaal Signalen" → `TOOLTIPS.totalSignals`
- "Gemiddelde Score" → `TOOLTIPS.avgScore`

### 4h. Live Price banner

Voeg tooltip toe aan het "Live Prijs" label:
```tsx
<p className="text-xs text-gray-500 mb-0.5 flex items-center">Live Prijs<InfoTooltip text={TOOLTIPS.livePrice} size={11} /></p>
```

En aan elk van de mini-stats rechts (Open, High, Low, Prev Close, Volume). Vervang elke `<span className="block text-gray-600">Label</span>` door:
```tsx
<span className="flex items-center text-gray-600">Label<InfoTooltip text={TOOLTIPS.xxx} size={11} /></span>
```

Gebruik de juiste keys: `open`, `high`, `low`, `prevClose`, `volume`.

### 4i. Score Trend sectie header

Vervang:
```tsx
<h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Score Trend</h2>
```
door:
```tsx
<h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Score Trend<InfoTooltip text={TOOLTIPS.scoreTrend} /></h2>
```

### 4j. Analisten consensus sectie header

Vervang:
```tsx
<h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Analisten consensus</h2>
```
door:
```tsx
<h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Analisten consensus<InfoTooltip text={TOOLTIPS.analystConsensus} /></h2>
```

En bij "Koersdoel analisten":
```tsx
<p className="text-xs text-gray-500 flex items-center">Koersdoel analisten<InfoTooltip text={TOOLTIPS.targetPrice} size={11} /></p>
```

---

## Stap 5: Pas `SignalsPage.tsx` aan

Import toevoegen:

```tsx
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 5a. BreakdownBar aanpassen

Vervang de `BreakdownBar` functie — voeg een optionele `tooltip` prop toe:

```tsx
function BreakdownBar({ label, score, tooltip }: { label: string; score: number | null; tooltip?: string }) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs text-gray-400 w-36 shrink-0 flex items-center">
        {label}
        {tooltip && <InfoTooltip text={tooltip} size={11} />}
      </span>
      {score !== null ? (
        <>
          <div className="flex-1 h-1.5 bg-gray-700 rounded-full overflow-hidden">
            <div
              className={`h-full rounded-full ${score >= 60 ? 'bg-green-500' : score >= 40 ? 'bg-amber-500' : 'bg-red-500'}`}
              style={{ width: `${Math.round(score)}%` }}
            />
          </div>
          <span className="font-mono text-xs text-gray-300 w-8 text-right">{Math.round(score)}%</span>
        </>
      ) : (
        <span className="text-xs text-gray-600">N/A</span>
      )}
    </div>
  );
}
```

Let op: de `w-32` wordt `w-36` om ruimte te maken voor het info-icoon.

### 5b. SignalDetail — breakdown bars met tooltips

Vervang de BreakdownBar calls in `SignalDetail`:

```tsx
<BreakdownBar label="Technical"    score={techNorm}   tooltip={TOOLTIPS.techScore} />
<BreakdownBar label="Sentiment"    score={sentNorm}    tooltip={TOOLTIPS.sentimentScore} />
<BreakdownBar label={claudeLabel}  score={claudeScore} tooltip={TOOLTIPS.claudeAI} />
<BreakdownBar label="ML"           score={mlScore}     tooltip={TOOLTIPS.mlScore} />
<BreakdownBar label="Fundamentals" score={null}        tooltip={TOOLTIPS.fundamentalsScore} />
```

### 5c. Status Indicatoren badges

Vervang de 4 status badge `<span>` elementen in `SignalDetail` door versies met InfoTooltip:

```tsx
{signal.trendStatus && (
  <span className="px-2 py-0.5 rounded bg-blue-500/20 text-blue-400 text-xs flex items-center gap-1">
    {signal.trendStatus}<InfoTooltip text={TOOLTIPS.trendStatus} size={11} />
  </span>
)}
{signal.momentumStatus && (
  <span className="px-2 py-0.5 rounded bg-purple-500/20 text-purple-400 text-xs flex items-center gap-1">
    {signal.momentumStatus}<InfoTooltip text={TOOLTIPS.momentumStatus} size={11} />
  </span>
)}
{signal.volatilityStatus && (
  <span className="px-2 py-0.5 rounded bg-amber-500/20 text-amber-400 text-xs flex items-center gap-1">
    {signal.volatilityStatus}<InfoTooltip text={TOOLTIPS.volatilityStatus} size={11} />
  </span>
)}
{signal.volumeStatus && (
  <span className="px-2 py-0.5 rounded bg-gray-600/50 text-gray-300 text-xs flex items-center gap-1">
    {signal.volumeStatus}<InfoTooltip text={TOOLTIPS.volumeStatus} size={11} />
  </span>
)}
```

---

## Stap 6: Pas `DashboardPage.tsx` aan

Import toevoegen:

```tsx
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 6a. StatCard aanpassen

Voeg `tooltip` prop toe aan StatCard:

```tsx
function StatCard({ icon, label, value, color, tooltip }: {
  icon: React.ReactNode;
  label: string;
  value: number;
  color: 'blue' | 'green' | 'red' | 'purple' | 'amber';
  tooltip?: string;
}) {
  const colors = {
    blue:   'bg-blue-500/10   text-blue-400   border-blue-500/20',
    green:  'bg-green-500/10  text-green-400  border-green-500/20',
    red:    'bg-red-500/10    text-red-400    border-red-500/20',
    purple: 'bg-purple-500/10 text-purple-400 border-purple-500/20',
    amber:  'bg-amber-500/10  text-amber-400  border-amber-500/20',
  };
  return (
    <div className={`rounded-xl border p-5 ${colors[color]}`}>
      <div className="flex items-center gap-2 mb-2 opacity-80">
        {icon}
        <span className="text-sm">{label}</span>
        {tooltip && <InfoTooltip text={tooltip} />}
      </div>
      <div className="text-3xl font-bold">{value}</div>
    </div>
  );
}
```

### 6b. StatCard calls aanpassen

```tsx
<StatCard icon={<Eye size={20} />}          label="Watchlist"      value={d.watchlistCount}       color="blue"   tooltip={TOOLTIPS.watchlistCount} />
<StatCard icon={<Briefcase size={20} />}    label="Portfolio"      value={d.portfolioPositions}   color="purple" tooltip={TOOLTIPS.portfolioPositions} />
<StatCard icon={<TrendingUp size={20} />}   label="BUY deze week"  value={d.signals.weekBuys}     color="green"  tooltip={TOOLTIPS.weekBuys} />
<StatCard icon={<TrendingDown size={20} />} label="SELL deze week" value={d.signals.weekSells}    color="red"    tooltip={TOOLTIPS.weekSells} />
<StatCard icon={<Zap size={20} />}          label="SQUEEZE"        value={d.signals.weekSqueezes} color="amber"  tooltip={TOOLTIPS.weekSqueezes} />
```

### 6c. Sector Sentiment heading

```tsx
<h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
  Sector Sentiment
  <InfoTooltip text={TOOLTIPS.sectorSentiment} />
</h3>
```

### 6d. Trending heading

```tsx
<h3 className="text-lg font-semibold text-white flex items-center gap-2">Trending<InfoTooltip text={TOOLTIPS.trending} /></h3>
```

---

## Stap 7: Pas `MarketsPage.tsx` aan

Import toevoegen:

```tsx
import { InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 7a. SignalBadge met tooltip

Vervang in het `SignalBadge` component de return statement door:

```tsx
return (
  <span className={`px-1.5 py-0.5 rounded text-[10px] font-semibold inline-flex items-center gap-0.5 ${color}`}>
    {signal.finalVerdict} {pct}%
    <InfoTooltip text={TOOLTIPS.signalBadge} size={10} />
  </span>
);
```

---

## Stap 8: Pas `PortfolioPage.tsx` aan

Import toevoegen:

```tsx
import { VerdictBadge, SymbolSearch, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 8a. Portefeuillewaarde card

Vervang `<p className="text-xs text-gray-500 mb-1">Portefeuillewaarde</p>` door:
```tsx
<p className="text-xs text-gray-500 mb-1 flex items-center">Portefeuillewaarde<InfoTooltip text={TOOLTIPS.portfolioValue} /></p>
```

### 8b. Allocatie heading

Vervang `<p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Allocatie</p>` door:
```tsx
<p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Allocatie<InfoTooltip text={TOOLTIPS.allocation} /></p>
```

---

## Stap 9: Pas `WatchlistPage.tsx` aan

Import toevoegen:

```tsx
import { VerdictBadge, ScoreBar, SymbolSearch, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';
```

### 9a. Market Cap label

Vervang `<span className="text-xs text-gray-500">Market Cap</span>` door:
```tsx
<span className="text-xs text-gray-500 flex items-center">Market Cap<InfoTooltip text={TOOLTIPS.watchlistMarketCap} size={11} /></span>
```

---

## Samenvatting gewijzigde/nieuwe bestanden

| Bestand | Actie |
|---------|-------|
| `frontend/src/components/shared/InfoTooltip.tsx` | **Nieuw** |
| `frontend/src/components/shared/index.ts` | **Gewijzigd** — export InfoTooltip |
| `frontend/src/utils/tooltipTexts.ts` | **Nieuw** |
| `frontend/src/pages/StockDetailPage.tsx` | **Gewijzigd** — tooltips op alle metrics |
| `frontend/src/pages/SignalsPage.tsx` | **Gewijzigd** — tooltips op breakdown + status |
| `frontend/src/pages/DashboardPage.tsx` | **Gewijzigd** — tooltips op stat cards + sector sentiment |
| `frontend/src/pages/MarketsPage.tsx` | **Gewijzigd** — tooltip op signal badge |
| `frontend/src/pages/PortfolioPage.tsx` | **Gewijzigd** — tooltips op waarde + allocatie |
| `frontend/src/pages/WatchlistPage.tsx` | **Gewijzigd** — tooltip op market cap |
