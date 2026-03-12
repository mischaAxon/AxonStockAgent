# Prompt 18 — Markets Trader Grid: Compact overzicht met beurs-kolommen

## Doel

Herschrijf de Markets-pagina naar een dense, Bloomberg-achtig trader grid. Beurzen worden als verticale kolommen naast elkaar getoond. Elke symbool is een klein compact vierkant tegel (~100x80px). Alles op één scherm zichtbaar, maximale informatiedichtheid.

## Verificatie achteraf

```bash
cd frontend
npx tsc --noEmit
npm run build
```

---

## Stap 1: Vervang `frontend/src/pages/MarketsPage.tsx` volledig

Vervang de **volledige inhoud** van het bestand door:

```tsx
import { useState, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search } from 'lucide-react';
import { useAllSymbols, useBatchQuotes, useLatestSignalsPerSymbol } from '../hooks/useApi';
import type { MarketSymbol, Quote, LatestSignalPerSymbol } from '../types';

// ── helpers ──────────────────────────────────────────────────────────────────

function countryFlag(code: string | null): string {
  if (!code || code.length !== 2) return '\uD83C\uDF10';
  return code.toUpperCase().replace(/./g, c =>
    String.fromCodePoint(c.charCodeAt(0) + 127397));
}

function formatPrice(price: number): string {
  if (price >= 10000) return price.toLocaleString('nl-NL', { maximumFractionDigits: 0 });
  if (price >= 1000) return price.toLocaleString('nl-NL', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  if (price >= 100) return price.toFixed(1);
  return price.toFixed(2);
}

function shortSymbol(symbol: string): string {
  // Strip exchange suffix: "ASML.AS" → "ASML", "AAPL.US" → "AAPL"
  const dot = symbol.indexOf('.');
  return dot > 0 ? symbol.substring(0, dot) : symbol;
}

type VerdictFilter = '' | 'BUY' | 'SELL' | 'SQUEEZE';

// ── SymbolSearchDropdown ─────────────────────────────────────────────────────

function SymbolSearchDropdown({
  symbols,
  onSelect,
}: {
  symbols: MarketSymbol[];
  onSelect: (symbol: string) => void;
}) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const filtered = useMemo(() => {
    if (!query.trim()) return [];
    const q = query.toLowerCase();
    return symbols
      .filter(s =>
        s.symbol.toLowerCase().includes(q) ||
        shortSymbol(s.symbol).toLowerCase().includes(q) ||
        (s.name?.toLowerCase().includes(q))
      )
      .slice(0, 10);
  }, [symbols, query]);

  return (
    <div ref={ref} className="relative">
      <div className="relative">
        <Search size={14} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-500" />
        <input
          type="text"
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => { if (query.trim()) setOpen(true); }}
          onBlur={() => setTimeout(() => setOpen(false), 200)}
          placeholder="Zoek..."
          className="w-52 pl-8 pr-3 py-1.5 rounded-md bg-gray-900 border border-gray-800 text-xs text-white placeholder-gray-600 focus:border-axon-500 focus:outline-none transition-colors"
        />
      </div>
      {open && filtered.length > 0 && (
        <div className="absolute z-50 mt-1 w-72 bg-gray-900 border border-gray-700 rounded-lg shadow-xl overflow-hidden">
          {filtered.map(s => (
            <button
              key={s.symbol}
              onMouseDown={() => {
                onSelect(s.symbol);
                setQuery('');
                setOpen(false);
              }}
              className="w-full flex items-center gap-2 px-3 py-1.5 hover:bg-gray-800 transition-colors text-left"
            >
              <span className="font-mono text-xs font-bold text-axon-400 w-16">{shortSymbol(s.symbol)}</span>
              <span className="text-xs text-gray-400 truncate">{s.name}</span>
              <span className="text-[10px] text-gray-600 ml-auto">{s.exchange}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ── Tile ─────────────────────────────────────────────────────────────────────

function Tile({
  symbol,
  quote,
  signal,
  onClick,
}: {
  symbol: MarketSymbol;
  quote: Quote | undefined;
  signal: LatestSignalPerSymbol | undefined;
  onClick: () => void;
}) {
  // Background color based on signal or change
  let bg = 'bg-gray-900/80';
  let border = 'border-gray-800/60';

  if (signal) {
    if (signal.finalVerdict === 'BUY') {
      bg = 'bg-green-950/40';
      border = 'border-green-500/30';
    } else if (signal.finalVerdict === 'SELL') {
      bg = 'bg-red-950/40';
      border = 'border-red-500/30';
    } else if (signal.finalVerdict === 'SQUEEZE') {
      bg = 'bg-yellow-950/40';
      border = 'border-yellow-500/30';
    }
  } else if (quote) {
    if (quote.changePercent > 1) {
      bg = 'bg-green-950/20';
    } else if (quote.changePercent < -1) {
      bg = 'bg-red-950/20';
    }
  }

  const changePct = quote?.changePercent ?? 0;
  const changeColor = changePct > 0 ? 'text-green-400' : changePct < 0 ? 'text-red-400' : 'text-gray-500';

  return (
    <div
      onClick={onClick}
      className={`${bg} border ${border} rounded-md p-1.5 cursor-pointer hover:brightness-125 transition-all select-none`}
      title={`${symbol.symbol} — ${symbol.name ?? ''}${signal ? ` | ${signal.finalVerdict} ${Math.round(signal.finalScore * 100)}%` : ''}`}
    >
      {/* Symbol ticker */}
      <div className="font-mono text-[11px] font-bold text-white leading-none truncate">
        {shortSymbol(symbol.symbol)}
      </div>

      {/* Price */}
      <div className="font-mono text-[10px] text-gray-300 leading-tight mt-0.5">
        {quote ? formatPrice(quote.currentPrice) : '—'}
      </div>

      {/* Change % */}
      <div className={`font-mono text-[10px] font-semibold leading-tight ${changeColor}`}>
        {quote ? `${changePct > 0 ? '+' : ''}${changePct.toFixed(1)}%` : ''}
      </div>

      {/* Signal indicator dot */}
      {signal && (
        <div className="flex items-center gap-0.5 mt-0.5">
          <span className={`w-1.5 h-1.5 rounded-full ${
            signal.finalVerdict === 'BUY' ? 'bg-green-400'
              : signal.finalVerdict === 'SELL' ? 'bg-red-400'
              : 'bg-yellow-400'
          }`} />
          <span className="text-[8px] text-gray-500 font-mono">
            {Math.round(signal.finalScore * 100)}
          </span>
        </div>
      )}
    </div>
  );
}

// ── ExchangeColumn ───────────────────────────────────────────────────────────

function ExchangeColumn({
  exchange,
  country,
  symbols,
  quotes,
  signalMap,
  onSymbolClick,
}: {
  exchange: string;
  country: string;
  symbols: MarketSymbol[];
  quotes: Record<string, Quote>;
  signalMap: Record<string, LatestSignalPerSymbol>;
  onSymbolClick: (symbol: string) => void;
}) {
  return (
    <div className="flex flex-col min-w-[130px]">
      {/* Column header */}
      <div className="sticky top-0 z-10 bg-gray-950 pb-2 border-b border-gray-800 mb-2">
        <div className="flex items-center gap-1.5">
          <span className="text-sm">{countryFlag(country)}</span>
          <span className="text-xs font-bold text-white truncate">{exchange}</span>
        </div>
        <span className="text-[10px] text-gray-600">{symbols.length}</span>
      </div>

      {/* Symbol tiles — grid of small tiles */}
      <div className="grid gap-1" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(80px, 1fr))' }}>
        {symbols.map(sym => (
          <Tile
            key={sym.symbol}
            symbol={sym}
            quote={quotes[sym.symbol]}
            signal={signalMap[sym.symbol]}
            onClick={() => onSymbolClick(sym.symbol)}
          />
        ))}
      </div>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function MarketsPage() {
  const [search, setSearch] = useState('');
  const [verdictFilter, setVerdictFilter] = useState<VerdictFilter>('');

  const { data: symbolsData, isLoading } = useAllSymbols();
  const allSymbols: MarketSymbol[] = symbolsData?.data ?? [];

  const symbolTickers = useMemo(() => allSymbols.map(s => s.symbol), [allSymbols]);
  const { data: quotesData } = useBatchQuotes(symbolTickers);
  const quotes: Record<string, Quote> = quotesData?.data ?? {};

  const { data: signalsData } = useLatestSignalsPerSymbol(7);
  const signalMap = useMemo(() => {
    const map: Record<string, LatestSignalPerSymbol> = {};
    for (const sig of signalsData?.data ?? []) {
      map[sig.symbol] = sig;
    }
    return map;
  }, [signalsData]);

  const navigate = useNavigate();

  // Apply filters
  const filtered = useMemo(() => {
    let result = allSymbols;

    if (search.trim()) {
      const q = search.toLowerCase();
      result = result.filter(s =>
        s.symbol.toLowerCase().includes(q) ||
        shortSymbol(s.symbol).toLowerCase().includes(q) ||
        (s.name?.toLowerCase().includes(q)) ||
        (s.sector?.toLowerCase().includes(q))
      );
    }

    if (verdictFilter) {
      result = result.filter(s => {
        const sig = signalMap[s.symbol];
        return sig?.finalVerdict === verdictFilter;
      });
    }

    return result;
  }, [allSymbols, search, verdictFilter, signalMap]);

  // Group by exchange, sort alphabetically within each
  const exchangeGroups = useMemo(() => {
    const byExchange: Record<string, { symbols: MarketSymbol[]; country: string }> = {};

    for (const sym of filtered) {
      const exchange = sym.exchange ?? 'Other';
      if (!byExchange[exchange]) {
        byExchange[exchange] = { symbols: [], country: sym.country ?? 'XX' };
      }
      byExchange[exchange].symbols.push(sym);
    }

    return Object.entries(byExchange)
      .sort(([, a], [, b]) => {
        if (a.country === 'NL' && b.country !== 'NL') return -1;
        if (b.country === 'NL' && a.country !== 'NL') return 1;
        if (a.country === 'US' && b.country !== 'US') return -1;
        if (b.country === 'US' && a.country !== 'US') return 1;
        return 0;
      })
      .map(([exchange, { symbols, country }]) => ({
        exchange,
        country,
        symbols: symbols.sort((a, b) => shortSymbol(a.symbol).localeCompare(shortSymbol(b.symbol))),
      }));
  }, [filtered]);

  // Counts
  const counts = useMemo(() => {
    const c = { all: allSymbols.length, BUY: 0, SELL: 0, SQUEEZE: 0 };
    for (const sym of allSymbols) {
      const sig = signalMap[sym.symbol];
      if (sig?.finalVerdict === 'BUY') c.BUY++;
      else if (sig?.finalVerdict === 'SELL') c.SELL++;
      else if (sig?.finalVerdict === 'SQUEEZE') c.SQUEEZE++;
    }
    return c;
  }, [allSymbols, signalMap]);

  // Stats
  const gainers = Object.values(quotes).filter(q => q.changePercent > 0).length;
  const losers = Object.values(quotes).filter(q => q.changePercent < 0).length;

  return (
    <div className="h-full flex flex-col">
      {/* Compact header bar */}
      <div className="flex items-center justify-between gap-3 pb-3 border-b border-gray-800 mb-3 flex-shrink-0">
        <div className="flex items-center gap-4">
          <h1 className="text-lg font-bold text-white">Markets</h1>

          {/* Inline stats */}
          <div className="flex items-center gap-3 text-[11px]">
            <span className="text-gray-500">{allSymbols.length} sym</span>
            <span className="text-gray-700">|</span>
            <span className="text-green-400">{gainers} ▲</span>
            <span className="text-red-400">{losers} ▼</span>
            <span className="text-gray-700">|</span>
            <span className="text-gray-500">{Object.keys(quotes).length} live</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* Verdict filters — compact */}
          {([
            { key: '' as VerdictFilter, label: 'ALL', count: counts.all },
            { key: 'BUY' as VerdictFilter, label: 'BUY', count: counts.BUY },
            { key: 'SELL' as VerdictFilter, label: 'SELL', count: counts.SELL },
            { key: 'SQUEEZE' as VerdictFilter, label: 'SQZ', count: counts.SQUEEZE },
          ]).map(({ key, label, count }) => (
            <button
              key={key}
              onClick={() => setVerdictFilter(key)}
              className={`px-2 py-1 rounded text-[10px] font-bold transition-all ${
                verdictFilter === key
                  ? key === 'BUY' ? 'bg-green-500/20 text-green-400 ring-1 ring-green-500/50'
                    : key === 'SELL' ? 'bg-red-500/20 text-red-400 ring-1 ring-red-500/50'
                    : key === 'SQUEEZE' ? 'bg-yellow-500/20 text-yellow-400 ring-1 ring-yellow-500/50'
                    : 'bg-gray-700 text-white ring-1 ring-gray-600'
                  : 'bg-gray-900 text-gray-500 hover:text-gray-300'
              }`}
            >
              {label}
              {count > 0 && <span className="ml-1 opacity-60">{count}</span>}
            </button>
          ))}

          <div className="w-px h-5 bg-gray-800" />

          {/* Search */}
          <SymbolSearchDropdown
            symbols={allSymbols}
            onSelect={symbol => navigate(`/stock/${symbol}`)}
          />

          {/* Quick text filter input */}
          <div className="relative">
            <input
              type="text"
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Filter..."
              className="w-28 px-2 py-1.5 rounded-md bg-gray-900 border border-gray-800 text-[11px] text-white placeholder-gray-600 focus:border-axon-500 focus:outline-none"
            />
            {search && (
              <button
                onClick={() => setSearch('')}
                className="absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-500 hover:text-white text-xs"
              >
                ×
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Main grid area — exchanges as columns side by side */}
      {isLoading ? (
        <div className="flex gap-4 flex-1">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="flex-1 space-y-1">
              <div className="h-6 bg-gray-800 rounded w-24 animate-pulse" />
              <div className="grid grid-cols-3 gap-1">
                {Array.from({ length: 12 }).map((_, j) => (
                  <div key={j} className="h-16 bg-gray-900 border border-gray-800 rounded-md animate-pulse" />
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : exchangeGroups.length === 0 ? (
        <div className="flex-1 flex items-center justify-center">
          <p className="text-gray-500 text-sm">
            {search || verdictFilter
              ? 'Geen symbolen gevonden.'
              : 'Geen symbolen. Ga naar Admin → Beurzen om exchanges te importeren.'}
          </p>
        </div>
      ) : (
        <div className="flex gap-4 flex-1 overflow-x-auto pb-2" style={{ scrollbarWidth: 'thin' }}>
          {exchangeGroups.map(({ exchange, country, symbols }) => (
            <ExchangeColumn
              key={exchange}
              exchange={exchange}
              country={country}
              symbols={symbols}
              quotes={quotes}
              signalMap={signalMap}
              onSymbolClick={symbol => navigate(`/stock/${symbol}`)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
```

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `frontend/src/pages/MarketsPage.tsx` | **Herschreven** — trader grid layout |

### Wat is er veranderd t.o.v. het vorige design:

- **Beurs-kolommen naast elkaar** i.p.v. horizontale scroll-rijen. Elke beurs is een verticale kolom. Bij veel beurzen scroll je horizontaal over het hele scherm.
- **Compacte tegels (~80px breed)** per symbool i.p.v. grote 220px cards. Toont: ticker, prijs, change%, signaal-dot met score.
- **Achtergrondkleur per tegel** — subtiel groen/rood/geel op basis van signaal of koersverandering. Geen logo's (te veel ruimte).
- **Header bar in één regel** — stats (gainers/losers), filters (ALL/BUY/SELL/SQZ), zoek-dropdown en snelfilter allemaal op één lijn.
- **Sortering op alfabet** binnen elke kolom (op ticker, zonder exchange suffix).
- **shortSymbol()** helper — "ASML.AS" wordt "ASML" op de tegel.
- **Hover tooltip** met volledige naam + signaalinfo.
- **Maximale informatiedichtheid** — past 50+ symbolen op één scherm.
