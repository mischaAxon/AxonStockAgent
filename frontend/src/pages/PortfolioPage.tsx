import { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { usePortfolio, useUpsertPortfolio, useDeletePortfolio, useLatestSignals } from '../hooks/useApi';
import { Plus, Trash2, Briefcase } from 'lucide-react';
import type { PortfolioItem, Signal } from '../types';
import { relativeTime } from '../utils/formatTime';
import { VerdictBadge, SymbolSearch, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';

// ─── helpers ────────────────────────────────────────────────────────────────

const ALLOC_COLORS = [
  'bg-blue-500', 'bg-green-500', 'bg-amber-500', 'bg-purple-500',
  'bg-red-500', 'bg-cyan-500', 'bg-pink-500', 'bg-indigo-500',
  'bg-orange-500', 'bg-teal-500',
];

// Dot version of ALLOC_COLORS for the legend
const ALLOC_DOT_COLORS = [
  'bg-blue-500', 'bg-green-500', 'bg-amber-500', 'bg-purple-500',
  'bg-red-500', 'bg-cyan-500', 'bg-pink-500', 'bg-indigo-500',
  'bg-orange-500', 'bg-teal-500',
];

// ─── main page ───────────────────────────────────────────────────────────────

export default function PortfolioPage() {
  const { data, isLoading }  = usePortfolio();
  const upsertMutation       = useUpsertPortfolio();
  const deleteMutation       = useDeletePortfolio();
  const { data: signalsData } = useLatestSignals(50);

  const [form, setForm] = useState({ symbol: '', shares: '', price: '', notes: '' });

  const items: PortfolioItem[] = data?.data ?? [];
  const latestSignals: Signal[] = signalsData?.data ?? [];

  const signalMap = useMemo(() => {
    const map = new Map<string, Signal>();
    for (const s of latestSignals) {
      if (!map.has(s.symbol)) map.set(s.symbol, s);
    }
    return map;
  }, [latestSignals]);

  const totalValue = items.reduce((sum, p) => sum + p.shares * (p.avgBuyPrice ?? 0), 0);

  const allocations = useMemo(() => {
    if (totalValue === 0) return [];
    return items
      .map(p => ({
        symbol: p.symbol,
        value: p.shares * (p.avgBuyPrice ?? 0),
        pct: ((p.shares * (p.avgBuyPrice ?? 0)) / totalValue) * 100,
      }))
      .sort((a, b) => b.value - a.value);
  }, [items, totalValue]);

  function handleAdd() {
    if (!form.symbol || !form.shares) return;
    upsertMutation.mutate({
      symbol: form.symbol.trim().toUpperCase(),
      shares: parseInt(form.shares),
      avgBuyPrice: form.price ? parseFloat(form.price) : undefined,
      notes: form.notes || undefined,
    });
    setForm({ symbol: '', shares: '', price: '', notes: '' });
  }

  function handleDelete(symbol: string) {
    if (window.confirm(`Positie ${symbol} verwijderen?`)) {
      deleteMutation.mutate(symbol);
    }
  }

  const signalsWithData = items.filter(p => signalMap.has(p.symbol)).length;

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Portfolio</h2>

      {/* Add position form */}
      <div className="flex gap-2 mb-6 flex-wrap">
        <SymbolSearch
          value={form.symbol}
          onChange={symbol => setForm({ ...form, symbol })}
          placeholder="Zoek symbool…"
          className="w-64"
        />
        <input
          type="number"
          value={form.shares}
          onChange={e => setForm({ ...form, shares: e.target.value })}
          placeholder="Aantal"
          className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-400 w-28 text-sm"
        />
        <input
          type="number"
          step="0.01"
          value={form.price}
          onChange={e => setForm({ ...form, price: e.target.value })}
          placeholder="Gem. prijs"
          className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-400 w-32 text-sm"
        />
        <input
          type="text"
          value={form.notes}
          onChange={e => setForm({ ...form, notes: e.target.value })}
          placeholder="Notities (optioneel)"
          className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-400 w-48 text-sm"
        />
        <button
          onClick={handleAdd}
          disabled={upsertMutation.isPending}
          className="bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-medium transition-colors"
        >
          <Plus size={16} /> Toevoegen
        </button>
      </div>

      {isLoading ? (
        <p className="text-gray-400 text-sm">Laden...</p>
      ) : items.length === 0 ? (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-16 text-center">
          <Briefcase size={40} className="text-gray-700 mx-auto mb-4" />
          <p className="text-white font-medium mb-1">Geen posities</p>
          <p className="text-sm text-gray-500 max-w-sm mx-auto">
            Voeg je eerste positie toe met het formulier hierboven.
          </p>
        </div>
      ) : (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-5">
              <p className="text-xs text-gray-500 mb-1 flex items-center">Portefeuillewaarde<InfoTooltip text={TOOLTIPS.portfolioValue} /></p>
              <p className="text-2xl font-bold text-white">
                €{totalValue.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </p>
            </div>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-5">
              <p className="text-xs text-gray-500 mb-1">Posities</p>
              <p className="text-2xl font-bold text-white">{items.length}</p>
            </div>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-5">
              <p className="text-xs text-gray-500 mb-1">Posities met signaal</p>
              <p className="text-2xl font-bold text-white">{signalsWithData}</p>
            </div>
          </div>

          {/* Allocatie bar */}
          {allocations.length > 0 && (
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Allocatie<InfoTooltip text={TOOLTIPS.allocation} /></p>

              {/* Stacked bar */}
              <div className="flex h-3 rounded-full overflow-hidden mb-3">
                {allocations.map((a, i) => (
                  <div
                    key={a.symbol}
                    title={`${a.symbol}: ${a.pct.toFixed(1)}%`}
                    className={`${ALLOC_COLORS[i % ALLOC_COLORS.length]} transition-all`}
                    style={{ width: `${a.pct}%` }}
                  />
                ))}
              </div>

              {/* Legend */}
              <div className="flex flex-wrap gap-3">
                {allocations.map((a, i) => (
                  <div key={a.symbol} className="flex items-center gap-1.5">
                    <span className={`w-2 h-2 rounded-full ${ALLOC_DOT_COLORS[i % ALLOC_DOT_COLORS.length]}`} />
                    <span className="text-xs font-mono text-gray-300">{a.symbol}</span>
                    <span className="text-xs text-gray-500">{a.pct.toFixed(1)}%</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Positions table */}
          <div className="bg-gray-900 rounded-xl border border-gray-800 overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-800/50">
                <tr>
                  <th className="px-4 py-3 text-left text-gray-400 font-medium">Symbool</th>
                  <th className="px-4 py-3 text-right text-gray-400 font-medium">Aandelen</th>
                  <th className="px-4 py-3 text-right text-gray-400 font-medium">Gem. Prijs</th>
                  <th className="px-4 py-3 text-right text-gray-400 font-medium">Waarde</th>
                  <th className="px-4 py-3 text-right text-gray-400 font-medium hidden md:table-cell">Alloc.</th>
                  <th className="px-4 py-3 text-left text-gray-400 font-medium">Signaal</th>
                  <th className="px-4 py-3 text-center text-gray-400 font-medium w-12"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-800">
                {items.map((p, i) => {
                  const value = p.shares * (p.avgBuyPrice ?? 0);
                  const pct   = totalValue > 0 ? (value / totalValue) * 100 : 0;
                  const signal = signalMap.get(p.symbol);
                  const colorIdx = allocations.findIndex(a => a.symbol === p.symbol);

                  return (
                    <tr key={p.id} className="hover:bg-gray-800/30">
                      <td className="px-4 py-3">
                        <Link
                          to={`/stock/${p.symbol}`}
                          className="font-mono font-bold text-axon-400 hover:text-axon-300 transition-colors"
                        >
                          {p.symbol}
                        </Link>
                        {p.notes && (
                          <p className="text-xs text-gray-500 mt-0.5 truncate max-w-xs">{p.notes}</p>
                        )}
                      </td>
                      <td className="px-4 py-3 text-right text-gray-300">{p.shares.toLocaleString()}</td>
                      <td className="px-4 py-3 text-right text-gray-300 font-mono">
                        {p.avgBuyPrice != null ? `€${p.avgBuyPrice.toFixed(2)}` : '—'}
                      </td>
                      <td className="px-4 py-3 text-right font-medium text-white font-mono">
                        {p.avgBuyPrice != null ? `€${value.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '—'}
                      </td>
                      <td className="px-4 py-3 text-right hidden md:table-cell">
                        <div className="flex items-center justify-end gap-2">
                          <div className="w-12 h-1 bg-gray-700 rounded-full overflow-hidden">
                            <div
                              className={`h-full rounded-full ${ALLOC_COLORS[colorIdx >= 0 ? colorIdx % ALLOC_COLORS.length : i % ALLOC_COLORS.length]}`}
                              style={{ width: `${pct}%` }}
                            />
                          </div>
                          <span className="font-mono text-xs text-gray-400">{pct.toFixed(1)}%</span>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        {signal ? (
                          <div>
                            <div className="flex items-center gap-1.5">
                              <VerdictBadge verdict={signal.finalVerdict} />
                              <span className="font-mono text-xs text-gray-500">
                                {(signal.finalScore * 100).toFixed(0)}%
                              </span>
                            </div>
                            <p className="text-xs text-gray-600 mt-0.5">{relativeTime(signal.createdAt)}</p>
                          </div>
                        ) : (
                          <span className="text-gray-600 text-xs">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-center">
                        <button
                          onClick={() => handleDelete(p.symbol)}
                          disabled={deleteMutation.isPending}
                          className="text-gray-600 hover:text-red-400 transition-colors p-1"
                          title={`Verwijder ${p.symbol}`}
                        >
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
