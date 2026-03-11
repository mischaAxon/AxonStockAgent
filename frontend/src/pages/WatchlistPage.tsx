import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useWatchlist, useAddToWatchlist, useRemoveFromWatchlist, useLatestSignals } from '../hooks/useApi';
import { Plus, X } from 'lucide-react';
import type { WatchlistItem, Signal } from '../types';
import { relativeTime } from '../utils/formatTime';

// ─── helpers ────────────────────────────────────────────────────────────────

function countryFlag(code: string | null): string {
  if (!code || code.length !== 2) return '';
  return code.toUpperCase().replace(/./g, c =>
    String.fromCodePoint(c.charCodeAt(0) + 127397));
}

function fmtLarge(val: number | null | undefined): string {
  if (val == null) return '—';
  if (Math.abs(val) >= 1e12) return '$' + (val / 1e12).toFixed(1) + 'T';
  if (Math.abs(val) >= 1e9)  return '$' + (val / 1e9).toFixed(1)  + 'B';
  if (Math.abs(val) >= 1e6)  return '$' + (val / 1e6).toFixed(1)  + 'M';
  return '$' + val.toFixed(0);
}

const SECTOR_COLORS: Record<string, string> = {
  'Technology':               'bg-blue-500/15 text-blue-400',
  'Healthcare':               'bg-green-500/15 text-green-400',
  'Financials':               'bg-emerald-500/15 text-emerald-400',
  'Finance':                  'bg-emerald-500/15 text-emerald-400',
  'Consumer Staples':         'bg-orange-500/15 text-orange-400',
  'Consumer Discretionary':   'bg-amber-500/15 text-amber-400',
  'Energy':                   'bg-yellow-500/15 text-yellow-400',
  'Industrials':              'bg-slate-500/15 text-slate-400',
  'Materials':                'bg-stone-500/15 text-stone-400',
  'Real Estate':              'bg-rose-500/15 text-rose-400',
  'Utilities':                'bg-cyan-500/15 text-cyan-400',
  'Communication Services':   'bg-purple-500/15 text-purple-400',
};

function sectorColor(sector: string | null) {
  return SECTOR_COLORS[sector ?? ''] ?? 'bg-gray-500/15 text-gray-400';
}

function VerdictBadge({ verdict }: { verdict: string }) {
  const styles: Record<string, string> = {
    BUY:     'bg-green-500/20 text-green-400',
    SELL:    'bg-red-500/20 text-red-400',
    SQUEEZE: 'bg-amber-500/20 text-amber-400',
  };
  return (
    <span className={`px-2 py-0.5 rounded text-xs font-bold ${styles[verdict] ?? 'bg-gray-700 text-gray-300'}`}>
      {verdict}
    </span>
  );
}

function ScoreBar({ score }: { score: number }) {
  const pct = Math.round(score * 100);
  const color = score >= 0.6 ? 'bg-green-500' : score >= 0.3 ? 'bg-amber-500' : 'bg-red-500';
  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-1.5 bg-gray-700 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="font-mono text-xs text-gray-300 w-8 text-right">{pct}%</span>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

type SortBy = 'name' | 'sector' | 'signal' | 'marketCap';

export default function WatchlistPage() {
  const { data, isLoading }  = useWatchlist();
  const addMutation          = useAddToWatchlist();
  const removeMutation       = useRemoveFromWatchlist();
  const { data: signalsData } = useLatestSignals(50);

  const [newSymbol, setNewSymbol]       = useState('');
  const [sectorFilter, setSectorFilter] = useState('');
  const [sortBy, setSortBy]             = useState<SortBy>('name');

  const items: WatchlistItem[] = data?.data ?? [];
  const latestSignals: Signal[] = signalsData?.data ?? [];

  const sectors = [...new Set(items.map(i => i.sector).filter(Boolean))] as string[];

  const filtered = sectorFilter
    ? items.filter(i => i.sector === sectorFilter)
    : items;

  // Map symbol → most recent signal
  const signalMap = useMemo(() => {
    const map = new Map<string, Signal>();
    for (const s of latestSignals) {
      if (!map.has(s.symbol)) map.set(s.symbol, s);
    }
    return map;
  }, [latestSignals]);

  const sorted = useMemo(() => {
    const arr = [...filtered];
    switch (sortBy) {
      case 'name':
        return arr.sort((a, b) => a.symbol.localeCompare(b.symbol));
      case 'sector':
        return arr.sort((a, b) => (a.sector ?? 'ZZZ').localeCompare(b.sector ?? 'ZZZ'));
      case 'signal':
        return arr.sort((a, b) => {
          const sa = signalMap.get(a.symbol);
          const sb = signalMap.get(b.symbol);
          if (!sa && !sb) return 0;
          if (!sa) return 1;
          if (!sb) return -1;
          return new Date(sb.createdAt).getTime() - new Date(sa.createdAt).getTime();
        });
      case 'marketCap':
        return arr.sort((a, b) => (b.marketCap ?? 0) - (a.marketCap ?? 0));
      default:
        return arr;
    }
  }, [filtered, sortBy, signalMap]);

  function handleAdd() {
    if (!newSymbol.trim()) return;
    addMutation.mutate({ symbol: newSymbol.trim().toUpperCase() });
    setNewSymbol('');
  }

  return (
    <div>
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between mb-6 gap-3">
        <h2 className="text-2xl font-bold text-white">Watchlist</h2>
        <div className="flex flex-wrap gap-2">
          {sectors.length > 0 && (
            <select
              value={sectorFilter}
              onChange={e => setSectorFilter(e.target.value)}
              className="bg-gray-800 border border-gray-700 text-sm text-white rounded-lg px-3 py-2 focus:outline-none focus:border-axon-400 transition-colors"
            >
              <option value="">Alle sectoren</option>
              {sectors.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          )}
          <select
            value={sortBy}
            onChange={e => setSortBy(e.target.value as SortBy)}
            className="bg-gray-800 border border-gray-700 text-sm text-white rounded-lg px-3 py-2 focus:outline-none focus:border-axon-400 transition-colors"
          >
            <option value="name">Sorteer: Naam</option>
            <option value="sector">Sorteer: Sector</option>
            <option value="signal">Sorteer: Laatste signaal</option>
            <option value="marketCap">Sorteer: Market Cap</option>
          </select>
        </div>
      </div>

      {/* Symbol toevoegen */}
      <div className="flex gap-2 mb-4">
        <input
          type="text"
          value={newSymbol}
          onChange={e => setNewSymbol(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleAdd()}
          placeholder="Symbol toevoegen (bijv. ASML.AS)"
          className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-400 w-72 text-sm"
        />
        <button
          onClick={handleAdd}
          disabled={addMutation.isPending}
          className="bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-medium transition-colors"
        >
          <Plus size={16} /> Toevoegen
        </button>
      </div>

      {/* Stats rij */}
      {!isLoading && items.length > 0 && (
        <div className="flex flex-wrap gap-4 mb-4 text-sm">
          <span className="text-gray-400">{items.length} symbolen</span>
          <span className="text-gray-600">·</span>
          <span className="text-green-400">
            {items.filter(i => signalMap.get(i.symbol)?.finalVerdict === 'BUY').length} BUY
          </span>
          <span className="text-red-400">
            {items.filter(i => signalMap.get(i.symbol)?.finalVerdict === 'SELL').length} SELL
          </span>
          <span className="text-amber-400">
            {items.filter(i => signalMap.get(i.symbol)?.finalVerdict === 'SQUEEZE').length} SQUEEZE
          </span>
        </div>
      )}

      {isLoading ? (
        <p className="text-gray-400 text-sm">Laden…</p>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {sorted.map(item => {
            const signal = signalMap.get(item.symbol);

            return (
              <div key={item.id} className="bg-gray-900 border border-gray-800 rounded-xl p-4">
                {/* Bovenste rij: logo + symbool + land + verwijder */}
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-3 min-w-0">
                    {item.logo ? (
                      <img
                        src={item.logo}
                        alt={item.name ?? item.symbol}
                        className="w-8 h-8 rounded-lg object-contain bg-white/5 p-0.5 flex-shrink-0"
                        onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
                      />
                    ) : (
                      <div className="w-8 h-8 rounded-lg bg-gray-800 flex items-center justify-center flex-shrink-0">
                        <span className="text-xs text-gray-400 font-mono">
                          {item.symbol.slice(0, 2)}
                        </span>
                      </div>
                    )}
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <Link
                          to={`/stock/${item.symbol}`}
                          className="font-mono font-bold text-axon-400 hover:text-axon-300 cursor-pointer"
                        >
                          {item.symbol}
                        </Link>
                        {item.country && (
                          <span className="text-base leading-none">{countryFlag(item.country)}</span>
                        )}
                      </div>
                      {item.name && (
                        <p className="text-xs text-gray-400 truncate mt-0.5">{item.name}</p>
                      )}
                    </div>
                  </div>
                  <button
                    onClick={() => removeMutation.mutate(item.symbol)}
                    className="text-gray-600 hover:text-red-400 transition-colors p-1 flex-shrink-0"
                  >
                    <X size={15} />
                  </button>
                </div>

                {/* Sector + Industry badges */}
                {(item.sector || item.industry) && (
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    {item.sector && (
                      <span className={`px-2 py-0.5 rounded text-xs font-medium ${sectorColor(item.sector)}`}>
                        {item.sector}
                      </span>
                    )}
                    {item.industry && (
                      <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-400">
                        {item.industry}
                      </span>
                    )}
                  </div>
                )}

                {/* Signaal rij */}
                {signal ? (
                  <div className="mt-3 pt-3 border-t border-gray-800">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <VerdictBadge verdict={signal.finalVerdict} />
                        <span className="text-xs text-gray-500">{relativeTime(signal.createdAt)}</span>
                      </div>
                      <span className="font-mono text-xs text-gray-300">€{signal.priceAtSignal.toFixed(2)}</span>
                    </div>
                    <div className="mt-2">
                      <ScoreBar score={signal.finalScore} />
                    </div>
                  </div>
                ) : (
                  <div className="mt-3 pt-3 border-t border-gray-800">
                    <span className="text-xs text-gray-600">Geen signalen</span>
                  </div>
                )}

                {/* Market Cap */}
                {item.marketCap && (
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-xs text-gray-500">Market Cap</span>
                    <span className="text-xs text-gray-400 font-mono">{fmtLarge(item.marketCap)}</span>
                  </div>
                )}
              </div>
            );
          })}

          {sorted.length === 0 && (
            <p className="text-gray-500 text-sm col-span-full">
              {sectorFilter ? `Geen aandelen in sector "${sectorFilter}"` : 'Watchlist is leeg'}
            </p>
          )}
        </div>
      )}
    </div>
  );
}
