import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, ChevronDown, ChevronRight, TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { useAllSymbols, useBatchQuotes } from '../hooks/useApi';
import type { MarketSymbol, Quote } from '../types';

function countryFlag(code: string | null): string {
  if (!code || code.length !== 2) return '🌐';
  return code.toUpperCase().replace(/./g, c =>
    String.fromCodePoint(c.charCodeAt(0) + 127397));
}

function countryName(code: string | null): string {
  const map: Record<string, string> = {
    NL: 'Nederland', US: 'United States', DE: 'Duitsland', GB: 'United Kingdom',
    FR: 'Frankrijk', JP: 'Japan', CN: 'China', HK: 'Hong Kong', CA: 'Canada',
    AU: 'Australië', CH: 'Zwitserland', SE: 'Zweden', KR: 'Zuid-Korea',
    IN: 'India', BR: 'Brazilië', IT: 'Italië', ES: 'Spanje',
  };
  return map[code ?? ''] ?? code ?? 'Overig';
}

function formatPrice(price: number): string {
  return price >= 1000
    ? price.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : price.toFixed(2);
}

function PriceChange({ quote }: { quote: Quote | undefined }) {
  if (!quote) return <span className="text-gray-600 text-xs">—</span>;

  const pct = quote.changePercent;
  const isUp = pct > 0;
  const isDown = pct < 0;

  return (
    <div className="flex items-center gap-1.5">
      <span className="font-mono text-sm font-semibold text-white">
        {formatPrice(quote.currentPrice)}
      </span>
      <span className={`flex items-center gap-0.5 text-xs font-medium ${
        isUp ? 'text-green-400' : isDown ? 'text-red-400' : 'text-gray-500'
      }`}>
        {isUp ? <TrendingUp size={12} /> : isDown ? <TrendingDown size={12} /> : <Minus size={12} />}
        {isUp ? '+' : ''}{pct.toFixed(2)}%
      </span>
    </div>
  );
}

function SymbolRow({ symbol, quote, onClick }: {
  symbol: MarketSymbol;
  quote: Quote | undefined;
  onClick: () => void;
}) {
  return (
    <div
      onClick={onClick}
      className="flex items-center gap-3 px-4 py-2.5 hover:bg-gray-800/50 cursor-pointer transition-colors group"
    >
      {symbol.logo ? (
        <img
          src={symbol.logo}
          alt={symbol.symbol}
          className="w-8 h-8 rounded-lg object-contain bg-white/5 p-0.5 flex-shrink-0"
          onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
        />
      ) : (
        <div className="w-8 h-8 rounded-lg bg-gray-800 flex items-center justify-center flex-shrink-0">
          <span className="font-mono text-[10px] text-gray-500">{symbol.symbol.slice(0, 3)}</span>
        </div>
      )}

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="font-mono text-sm font-semibold text-white group-hover:text-axon-400 transition-colors">
            {symbol.symbol}
          </span>
          {symbol.sector && (
            <span className="px-1.5 py-0.5 rounded text-[10px] bg-gray-800 text-gray-500 hidden sm:inline">
              {symbol.sector}
            </span>
          )}
        </div>
        {symbol.name && (
          <p className="text-xs text-gray-500 truncate">{symbol.name}</p>
        )}
      </div>

      <div className="flex-shrink-0 text-right">
        <PriceChange quote={quote} />
      </div>
    </div>
  );
}

export default function MarketsPage() {
  const [search, setSearch] = useState('');
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  const { data: symbolsData, isLoading } = useAllSymbols();
  const allSymbols: MarketSymbol[] = symbolsData?.data ?? [];

  const symbolTickers = useMemo(() => allSymbols.map(s => s.symbol), [allSymbols]);
  const { data: quotesData } = useBatchQuotes(symbolTickers);
  const quotes: Record<string, Quote> = quotesData?.data ?? {};

  const navigate = useNavigate();

  const filtered = useMemo(() => {
    if (!search.trim()) return allSymbols;
    const q = search.toLowerCase();
    return allSymbols.filter(s =>
      s.symbol.toLowerCase().includes(q) ||
      (s.name?.toLowerCase().includes(q)) ||
      (s.sector?.toLowerCase().includes(q))
    );
  }, [allSymbols, search]);

  const grouped = useMemo(() => {
    const byCountry: Record<string, Record<string, MarketSymbol[]>> = {};

    for (const sym of filtered) {
      const country = sym.country ?? 'XX';
      const exchange = sym.exchange ?? 'Unknown';
      if (!byCountry[country]) byCountry[country] = {};
      if (!byCountry[country][exchange]) byCountry[country][exchange] = [];
      byCountry[country][exchange].push(sym);
    }

    const sortedCountries = Object.keys(byCountry).sort((a, b) => {
      if (a === 'NL') return -1;
      if (b === 'NL') return 1;
      if (a === 'US') return -1;
      if (b === 'US') return 1;
      return countryName(a).localeCompare(countryName(b));
    });

    return sortedCountries.map(country => ({
      country,
      exchanges: Object.entries(byCountry[country]).map(([exchange, symbols]) => ({
        exchange,
        symbols: symbols.sort((a, b) => a.symbol.localeCompare(b.symbol)),
      })),
    }));
  }, [filtered]);

  const toggleExchange = (key: string) =>
    setCollapsed(prev => ({ ...prev, [key]: !prev[key] }));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Markets</h1>
          <p className="text-sm text-gray-500 mt-1">
            {allSymbols.length} symbolen · {Object.keys(quotes).length} live quotes
          </p>
        </div>

        <div className="relative w-72">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
          <input
            type="text"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Zoek symbool, naam of sector..."
            className="w-full pl-9 pr-4 py-2 rounded-lg bg-gray-900 border border-gray-800 text-sm text-white placeholder-gray-600 focus:border-axon-500 focus:outline-none transition-colors"
          />
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="bg-gray-900 border border-gray-800 rounded-xl p-4 animate-pulse">
              <div className="h-6 bg-gray-800 rounded w-48 mb-3" />
              <div className="space-y-2">
                {Array.from({ length: 4 }).map((_, j) => (
                  <div key={j} className="h-10 bg-gray-800/50 rounded" />
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : grouped.length === 0 ? (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-8 text-center">
          <p className="text-gray-400">
            {search ? `Geen resultaten voor "${search}"` : 'Nog geen symbolen. Voeg symbolen toe via de Watchlist.'}
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          {grouped.map(({ country, exchanges }) => (
            <div key={country}>
              <div className="flex items-center gap-2 mb-3">
                <span className="text-2xl">{countryFlag(country)}</span>
                <h2 className="text-lg font-semibold text-white">{countryName(country)}</h2>
                <span className="text-xs text-gray-500">
                  {exchanges.reduce((acc, e) => acc + e.symbols.length, 0)} symbolen
                </span>
              </div>

              <div className="space-y-3">
                {exchanges.map(({ exchange, symbols }) => {
                  const key = `${country}-${exchange}`;
                  const isCollapsed = collapsed[key] ?? false;

                  return (
                    <div key={key} className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
                      <button
                        onClick={() => toggleExchange(key)}
                        className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-800/30 transition-colors"
                      >
                        <div className="flex items-center gap-2">
                          {isCollapsed
                            ? <ChevronRight size={16} className="text-gray-500" />
                            : <ChevronDown size={16} className="text-gray-500" />
                          }
                          <span className="text-sm font-semibold text-gray-300">{exchange}</span>
                          <span className="text-xs text-gray-600">({symbols.length})</span>
                        </div>
                      </button>

                      {!isCollapsed && (
                        <div className="border-t border-gray-800 divide-y divide-gray-800/50">
                          {symbols.map(sym => (
                            <SymbolRow
                              key={sym.symbol}
                              symbol={sym}
                              quote={quotes[sym.symbol]}
                              onClick={() => navigate(`/stock/${sym.symbol}`)}
                            />
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
