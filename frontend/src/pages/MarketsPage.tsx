import { useState, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Star } from 'lucide-react';
import { useAllSymbols, useBatchQuotes, useLatestSignalsPerSymbol, useIndicesWithSymbols, useFavorites, useToggleFavorite, useSentimentChanges } from '../hooks/useApi';
import type { MarketSymbol, Quote, LatestSignalPerSymbol, MarketIndex } from '../types';
import { PillarDots } from '../components/PillarScoreBar';

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
  const dot = symbol.indexOf('.');
  return dot > 0 ? symbol.substring(0, dot) : symbol;
}

type VerdictFilter = '' | 'BUY' | 'SELL' | 'SQUEEZE' | 'FAV';

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
  isFavorite,
  onToggleFavorite,
  sentimentChange,
}: {
  symbol: MarketSymbol;
  quote: Quote | undefined;
  signal: LatestSignalPerSymbol | undefined;
  onClick: () => void;
  isFavorite: boolean;
  onToggleFavorite: () => void;
  sentimentChange: number | undefined;
}) {
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
      className={`${bg} border ${border} rounded-md p-1.5 cursor-pointer hover:brightness-125 transition-all select-none group relative`}
      title={`${symbol.symbol} — ${symbol.name ?? ''}${signal ? ` | ${signal.finalVerdict} ${Math.round(signal.finalScore * 100)}%` : ''}`}
    >
      <button
        onClick={(e) => { e.stopPropagation(); onToggleFavorite(); }}
        className={`absolute top-0.5 right-0.5 p-0.5 rounded transition-all z-10 ${
          isFavorite ? 'text-yellow-400 opacity-100' : 'text-gray-600 opacity-0 group-hover:opacity-60 hover:!opacity-100'
        }`}
      >
        <Star size={10} fill={isFavorite ? 'currentColor' : 'none'} />
      </button>
      <div className="font-mono text-[11px] font-bold text-white leading-none truncate">
        {shortSymbol(symbol.symbol)}
      </div>
      <div className="font-mono text-[10px] text-gray-300 leading-tight mt-0.5">
        {quote ? formatPrice(quote.currentPrice) : '—'}
      </div>
      <div className={`font-mono text-[10px] font-semibold leading-tight ${changeColor}`}>
        {quote ? `${changePct > 0 ? '+' : ''}${changePct.toFixed(1)}%` : ''}
      </div>
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
          <PillarDots
            techScore={signal.techScore}
            sentimentScore={signal.sentimentScore}
            claudeConfidence={signal.claudeConfidence}
            fundamentalsScore={signal.fundamentalsScore}
          />
        </div>
      )}
      {sentimentChange != null && (
        <div className={`text-[8px] font-mono leading-tight ${
          sentimentChange > 0 ? 'text-green-400/70' : sentimentChange < 0 ? 'text-red-400/70' : 'text-gray-600'
        }`}>
          S:{sentimentChange > 0 ? '+' : ''}{sentimentChange.toFixed(1)}%
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
  favoriteSet,
  onToggleFavorite,
  sentimentMap,
}: {
  exchange: string;
  country: string;
  symbols: MarketSymbol[];
  quotes: Record<string, Quote>;
  signalMap: Record<string, LatestSignalPerSymbol>;
  onSymbolClick: (symbol: string) => void;
  favoriteSet: Set<string>;
  onToggleFavorite: (symbol: string) => void;
  sentimentMap: Map<string, number>;
}) {
  return (
    <div className="flex flex-col min-w-[130px]">
      <div className="sticky top-0 z-10 bg-gray-950 pb-2 border-b border-gray-800 mb-2">
        <div className="flex items-center gap-1.5">
          <span className="text-sm">{countryFlag(country)}</span>
          <span className="text-xs font-bold text-white truncate">{exchange}</span>
        </div>
        <span className="text-[10px] text-gray-600">{symbols.length}</span>
      </div>
      <div className="grid gap-1" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(80px, 1fr))' }}>
        {symbols.map(sym => (
          <Tile
            key={sym.symbol}
            symbol={sym}
            quote={quotes[sym.symbol]}
            signal={signalMap[sym.symbol]}
            onClick={() => onSymbolClick(sym.symbol)}
            isFavorite={favoriteSet.has(sym.symbol)}
            onToggleFavorite={() => onToggleFavorite(sym.symbol)}
            sentimentChange={sentimentMap.get(sym.symbol)}
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

  const { data: indicesData, isLoading: indicesLoading } = useIndicesWithSymbols();
  const { data: symbolsData, isLoading: symbolsLoading } = useAllSymbols();
  const isLoading = indicesLoading || symbolsLoading;

  const indices: MarketIndex[] = indicesData?.data ?? [];
  const allMarketSymbols: MarketSymbol[] = symbolsData?.data ?? [];

  // Combineer: alle symbolen uit indexen + alle MarketSymbols
  const allSymbols = useMemo(() => {
    const symbolSet = new Map<string, MarketSymbol>();
    for (const idx of indices) {
      for (const sym of idx.symbols) {
        symbolSet.set(sym.symbol, sym);
      }
    }
    for (const sym of allMarketSymbols) {
      if (!symbolSet.has(sym.symbol)) {
        symbolSet.set(sym.symbol, sym);
      }
    }
    return Array.from(symbolSet.values());
  }, [indices, allMarketSymbols]);

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

  const { data: favoritesData } = useFavorites();
  const toggleFavorite = useToggleFavorite();

  const favoriteSet = useMemo(() => {
    return new Set<string>(favoritesData?.data ?? []);
  }, [favoritesData]);

  const { data: sentimentData } = useSentimentChanges(7);
  const sentimentMap = useMemo(() => {
    const map = new Map<string, number>();
    for (const item of sentimentData?.data ?? []) {
      if (item.sentimentChange != null) {
        map.set(item.symbol, item.sentimentChange);
      }
    }
    return map;
  }, [sentimentData]);

  const navigate = useNavigate();

  // Group: indexen als kolommen, plus "Overig" voor symbolen zonder index
  const columnGroups = useMemo(() => {
    const indexedSymbols = new Set<string>();
    for (const idx of indices) {
      for (const sym of idx.symbols) {
        indexedSymbols.add(sym.symbol);
      }
    }

    type ColumnGroup = { key: string; label: string; country: string; symbols: MarketSymbol[] };
    const groups: ColumnGroup[] = [];

    // Index-kolommen
    for (const idx of indices) {
      let syms = idx.symbols;

      if (search.trim()) {
        const q = search.toLowerCase();
        syms = syms.filter(s =>
          s.symbol.toLowerCase().includes(q) ||
          shortSymbol(s.symbol).toLowerCase().includes(q) ||
          (s.name?.toLowerCase().includes(q)) ||
          (s.sector?.toLowerCase().includes(q))
        );
      }

      if (verdictFilter) {
        if (verdictFilter === 'FAV') {
          syms = syms.filter(s => favoriteSet.has(s.symbol));
        } else {
          syms = syms.filter(s => signalMap[s.symbol]?.finalVerdict === verdictFilter);
        }
      }

      if (syms.length > 0) {
        groups.push({
          key: idx.indexSymbol,
          label: idx.displayName,
          country: idx.country,
          symbols: syms.sort((a, b) => shortSymbol(a.symbol).localeCompare(shortSymbol(b.symbol))),
        });
      }
    }

    // "Overig" kolommen: symbolen in MarketSymbols die in geen enkele index zitten
    const ungrouped = allMarketSymbols.filter(s => !indexedSymbols.has(s.symbol));
    if (ungrouped.length > 0) {
      let filteredUngrouped = ungrouped;
      if (search.trim()) {
        const q = search.toLowerCase();
        filteredUngrouped = filteredUngrouped.filter(s =>
          s.symbol.toLowerCase().includes(q) ||
          shortSymbol(s.symbol).toLowerCase().includes(q) ||
          (s.name?.toLowerCase().includes(q))
        );
      }
      if (verdictFilter) {
        if (verdictFilter === 'FAV') {
          filteredUngrouped = filteredUngrouped.filter(s => favoriteSet.has(s.symbol));
        } else {
          filteredUngrouped = filteredUngrouped.filter(s => signalMap[s.symbol]?.finalVerdict === verdictFilter);
        }
      }

      const byExchange: Record<string, MarketSymbol[]> = {};
      for (const sym of filteredUngrouped) {
        const ex = sym.exchange ?? 'Other';
        if (!byExchange[ex]) byExchange[ex] = [];
        byExchange[ex].push(sym);
      }
      for (const [ex, syms] of Object.entries(byExchange)) {
        if (syms.length > 0) {
          groups.push({
            key: `other-${ex}`,
            label: `Overig ${ex}`,
            country: syms[0]?.country ?? 'XX',
            symbols: syms.sort((a, b) => shortSymbol(a.symbol).localeCompare(shortSymbol(b.symbol))),
          });
        }
      }
    }

    return groups;
  }, [indices, allMarketSymbols, search, verdictFilter, signalMap, favoriteSet]);

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

  const gainers = Object.values(quotes).filter(q => q.changePercent > 0).length;
  const losers = Object.values(quotes).filter(q => q.changePercent < 0).length;

  return (
    <div className="h-full flex flex-col">
      {/* Compact header bar */}
      <div className="flex items-center justify-between gap-3 pb-3 border-b border-gray-800 mb-3 flex-shrink-0">
        <div className="flex items-center gap-4">
          <h1 className="text-lg font-bold text-white">Markets</h1>
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
          {([
            { key: '' as VerdictFilter, label: 'ALL', count: counts.all },
            { key: 'FAV' as VerdictFilter, label: '★ FAV', count: favoriteSet.size },
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
                    : key === 'FAV' ? 'bg-yellow-500/20 text-yellow-400 ring-1 ring-yellow-500/50'
                    : 'bg-gray-700 text-white ring-1 ring-gray-600'
                  : 'bg-gray-900 text-gray-500 hover:text-gray-300'
              }`}
            >
              {label}
              {count > 0 && <span className="ml-1 opacity-60">{count}</span>}
            </button>
          ))}

          <div className="w-px h-5 bg-gray-800" />

          <SymbolSearchDropdown
            symbols={allSymbols}
            onSelect={symbol => navigate(`/stock/${symbol}`)}
          />

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

      {/* Main grid area */}
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
      ) : columnGroups.length === 0 ? (
        <div className="flex-1 flex items-center justify-center">
          <p className="text-gray-500 text-sm">
            {search || verdictFilter
              ? 'Geen symbolen gevonden.'
              : 'Geen indexen geconfigureerd. Ga naar Admin → Beurzen om indexen toe te voegen en te importeren.'}
          </p>
        </div>
      ) : (
        <div className="flex gap-4 flex-1 overflow-x-auto pb-2" style={{ scrollbarWidth: 'thin' }}>
          {columnGroups.map(({ key, label, country, symbols }) => (
            <ExchangeColumn
              key={key}
              exchange={label}
              country={country}
              symbols={symbols}
              quotes={quotes}
              signalMap={signalMap}
              onSymbolClick={symbol => navigate(`/stock/${symbol}`)}
              favoriteSet={favoriteSet}
              onToggleFavorite={(symbol) => toggleFavorite.mutate(symbol)}
              sentimentMap={sentimentMap}
            />
          ))}
        </div>
      )}
    </div>
  );
}
