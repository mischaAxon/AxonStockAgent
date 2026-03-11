import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Search, Zap, ChevronDown, ChevronUp, Info } from 'lucide-react';
import { useSignals } from '../hooks/useApi';
import type { Signal } from '../types';
import { relativeTime } from '../utils/formatTime';
import { VerdictBadge, ScoreBar } from '../components/shared';

type VerdictFilter = '' | 'BUY' | 'SELL' | 'SQUEEZE';
type Period = 'today' | 'week' | 'month' | 'all';

function sinceISO(period: Period): string | undefined {
  const now = new Date();
  if (period === 'today') { const d = new Date(now); d.setHours(0, 0, 0, 0); return d.toISOString(); }
  if (period === 'week')  { const d = new Date(now); d.setDate(d.getDate() - 7); return d.toISOString(); }
  if (period === 'month') { const d = new Date(now); d.setMonth(d.getMonth() - 1); return d.toISOString(); }
  return undefined;
}

// ── Sub-components ──────────────────────────────────────────────────────────────────────

function BreakdownBar({ label, score }: { label: string; score: number | null }) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs text-gray-400 w-32 shrink-0">{label}</span>
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

function ScoreExplainer({ signal }: { signal: Signal }) {
  const techNorm = ((signal.techScore + 1) / 2) * 100;
  const sentNorm = signal.sentimentScore !== null ? ((signal.sentimentScore + 1) / 2) * 100 : null;
  const finalPct = Math.round(signal.finalScore * 100);

  return (
    <div className="mt-4 p-3 bg-gray-900/60 rounded-lg border border-gray-700/50">
      <div className="flex items-center gap-2 mb-2">
        <Info size={12} className="text-axon-400" />
        <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Hoe wordt de score berekend?</p>
      </div>
      <p className="text-xs text-gray-400 leading-relaxed">
        De <span className="text-white font-medium">eindscore ({finalPct}%)</span> is een gewogen gemiddelde van alle actieve signaal-bronnen.
        Elke bron heeft een eigen gewicht dat instelbaar is via de admin-instellingen.
        Scores worden genormaliseerd naar een 0–100% schaal:
      </p>
      <ul className="mt-2 space-y-1 text-xs text-gray-400">
        <li className="flex items-start gap-2">
          <span className="text-red-400 mt-0.5">●</span>
          <span><span className="text-gray-300 font-medium">0–35%</span> → bearish signaal (SELL). Hoe lager, hoe sterker het verkoopsignaal.</span>
        </li>
        <li className="flex items-start gap-2">
          <span className="text-amber-400 mt-0.5">●</span>
          <span><span className="text-gray-300 font-medium">35–65%</span> → neutraal (HOLD). Geen duidelijk signaal, geen actie.</span>
        </li>
        <li className="flex items-start gap-2">
          <span className="text-green-400 mt-0.5">●</span>
          <span><span className="text-gray-300 font-medium">65–100%</span> → bullish signaal (BUY). Hoe hoger, hoe sterker het koopsignaal.</span>
        </li>
      </ul>
      <p className="mt-2 text-xs text-gray-500">
        <span className="text-gray-400 font-medium">Technical ({Math.round(techNorm)}%)</span> is gebaseerd op technische indicatoren (trend, momentum, volatiliteit).
        {sentNorm !== null && <> <span className="text-gray-400 font-medium">Sentiment ({Math.round(sentNorm)}%)</span> komt uit nieuwsanalyse.</>}
        {signal.claudeConfidence !== null && <> <span className="text-gray-400 font-medium">Claude AI</span> voegt een AI-beoordeling toe op basis van alle beschikbare data.</>}
        {' '}Bronnen met N/A zijn nog niet actief en worden niet meegewogen.
      </p>
    </div>
  );
}

function SignalDetail({ signal }: { signal: Signal }) {
  const techNorm   = ((signal.techScore + 1) / 2) * 100;
  const sentNorm   = signal.sentimentScore !== null ? ((signal.sentimentScore + 1) / 2) * 100 : null;
  const claudeScore = signal.claudeConfidence !== null ? signal.claudeConfidence * 100 : null;
  const claudeLabel = `Claude AI${signal.claudeDirection ? ` (${signal.claudeDirection})` : ''}`;
  const mlScore    = signal.mlProbability !== null ? signal.mlProbability * 100 : null;

  return (
    <div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Score breakdown */}
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Score Breakdown</p>
          <div className="space-y-2.5">
            <BreakdownBar label="Technical"    score={techNorm} />
            <BreakdownBar label="Sentiment"    score={sentNorm} />
            <BreakdownBar label={claudeLabel}  score={claudeScore} />
            <BreakdownBar label="ML"           score={mlScore} />
            <BreakdownBar label="Fundamentals" score={null} />
          </div>
        </div>

        {/* Status badges + Claude reasoning */}
        <div>
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Status Indicatoren</p>
          <div className="flex flex-wrap gap-2 mb-4">
            {signal.trendStatus      && <span className="px-2 py-0.5 rounded bg-blue-500/20   text-blue-400   text-xs">{signal.trendStatus}</span>}
            {signal.momentumStatus   && <span className="px-2 py-0.5 rounded bg-purple-500/20 text-purple-400 text-xs">{signal.momentumStatus}</span>}
            {signal.volatilityStatus && <span className="px-2 py-0.5 rounded bg-amber-500/20  text-amber-400  text-xs">{signal.volatilityStatus}</span>}
            {signal.volumeStatus     && <span className="px-2 py-0.5 rounded bg-gray-600/50   text-gray-300   text-xs">{signal.volumeStatus}</span>}
          </div>

          {signal.claudeReasoning && (
            <>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Claude Reasoning</p>
              <p className="text-sm text-gray-300 leading-relaxed">{signal.claudeReasoning}</p>
            </>
          )}
        </div>
      </div>

      {/* Score uitleg sectie */}
      <ScoreExplainer signal={signal} />
    </div>
  );
}

function EmptyState() {
  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl p-16 text-center">
      <Zap size={40} className="text-gray-700 mx-auto mb-4" />
      <p className="text-white font-medium mb-1">Nog geen signalen</p>
      <p className="text-sm text-gray-500 max-w-sm mx-auto">
        De scanner draait op de achtergrond. Signalen verschijnen hier zodra de eerste scan voltooid is.
      </p>
    </div>
  );
}

// ── Main page ───────────────────────────────────────────────────────────────────────

export default function SignalsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSymbol = searchParams.get('symbol') ?? '';

  const [page, setPage]         = useState(1);
  const [filter, setFilter]     = useState<VerdictFilter>('');
  const [search, setSearch]     = useState(initialSymbol);
  const [debouncedSearch, setDebouncedSearch] = useState(initialSymbol);
  const [period, setPeriod]     = useState<Period>('all');
  const [expandedId, setExpandedId] = useState<number | null>(null);

  // Debounce search input 300ms + sync to URL
  useEffect(() => {
    const t = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
      // Keep URL in sync: add/remove ?symbol= param
      if (search) {
        setSearchParams(prev => { prev.set('symbol', search.toUpperCase()); return prev; }, { replace: true });
      } else {
        setSearchParams(prev => { prev.delete('symbol'); return prev; }, { replace: true });
      }
    }, 300);
    return () => clearTimeout(t);
  }, [search, setSearchParams]);

  const sinceParam = sinceISO(period);
  const { data, isLoading } = useSignals(page, 20, debouncedSearch || undefined, filter || undefined, sinceParam);

  const signals = data?.data ?? [];

  const totalPages = data?.meta ? Math.ceil(data.meta.total / 20) : 1;
  const noFiltersActive = !filter && !debouncedSearch && period === 'all';
  const isEmpty = !isLoading && signals.length === 0 && noFiltersActive;

  function toggleExpand(id: number) {
    setExpandedId(prev => prev === id ? null : id);
  }

  return (
    <div>
      {/* Header row */}
      <div className="flex flex-wrap items-center justify-between gap-4 mb-4">
        <div className="flex items-center gap-3">
          <h2 className="text-2xl font-bold text-white">Signalen</h2>
          {data?.meta && (
            <span className="px-2.5 py-0.5 rounded-full bg-gray-800 text-gray-400 text-sm font-medium">
              {data.meta.total} totaal
            </span>
          )}
        </div>

        <div className="flex flex-wrap gap-2 items-center">
          {/* Verdict filter buttons */}
          {(['', 'BUY', 'SELL', 'SQUEEZE'] as VerdictFilter[]).map((v) => (
            <button
              key={v}
              onClick={() => { setFilter(v); setPage(1); }}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                filter === v ? 'bg-axon-600 text-white' : 'bg-gray-800 text-gray-400 hover:text-white'
              }`}
            >
              {v || 'Alle'}
            </button>
          ))}

          {/* Period dropdown */}
          <select
            value={period}
            onChange={e => { setPeriod(e.target.value as Period); setPage(1); }}
            className="bg-gray-800 text-gray-300 text-sm rounded-lg px-3 py-1.5 border border-gray-700 focus:outline-none focus:border-axon-400"
          >
            <option value="today">Vandaag</option>
            <option value="week">Deze week</option>
            <option value="month">Deze maand</option>
            <option value="all">Alles</option>
          </select>
        </div>
      </div>

      {/* Search bar */}
      <div className="relative mb-4">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
        <input
          type="text"
          placeholder="Zoek op symbool..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full sm:w-64 bg-gray-800 border border-gray-700 text-white text-sm rounded-lg pl-8 pr-3 py-2 focus:outline-none focus:border-axon-400 transition-colors"
        />
      </div>

      {isLoading ? (
        <p className="text-gray-400 text-sm">Laden...</p>
      ) : isEmpty ? (
        <EmptyState />
      ) : signals.length === 0 ? (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
          <p className="text-gray-500 text-sm">Geen signalen gevonden voor de geselecteerde filters.</p>
        </div>
      ) : (
        <div className="bg-gray-900 rounded-xl border border-gray-800 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Datum</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Symbool</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Verdict</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium w-44">Score</th>
                <th className="px-4 py-3 text-right text-gray-400 font-medium">Prijs</th>
                <th className="px-4 py-3 text-center text-gray-400 font-medium w-16">Details</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {signals.map((s) => (
                <React.Fragment key={s.id}>
                  <tr
                    onClick={() => toggleExpand(s.id)}
                    className="hover:bg-gray-800/30 cursor-pointer"
                  >
                    <td className="px-4 py-3 text-gray-400 whitespace-nowrap">{relativeTime(s.createdAt)}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-white">{s.symbol}</td>
                    <td className="px-4 py-3"><VerdictBadge verdict={s.finalVerdict} /></td>
                    <td className="px-4 py-3"><ScoreBar score={s.finalScore} /></td>
                    <td className="px-4 py-3 text-right font-mono text-white">€{s.priceAtSignal.toFixed(2)}</td>
                    <td className="px-4 py-3 text-center text-gray-500">
                      {expandedId === s.id
                        ? <ChevronUp size={14} className="inline" />
                        : <ChevronDown size={14} className="inline" />}
                    </td>
                  </tr>

                  {expandedId === s.id && (
                    <tr>
                      <td colSpan={6} className="px-6 py-5 bg-gray-800/40 border-t border-gray-700">
                        <SignalDetail signal={s} />
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))}
            </tbody>
          </table>

          {data?.meta && (
            <div className="flex items-center justify-between px-4 py-3 bg-gray-800/30 border-t border-gray-800">
              <span className="text-sm text-gray-500">
                Pagina {page} van {totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-3 py-1 rounded bg-gray-800 text-gray-400 disabled:opacity-30"
                >
                  Vorige
                </button>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page >= totalPages}
                  className="px-3 py-1 rounded bg-gray-800 text-gray-400 disabled:opacity-30"
                >
                  Volgende
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
