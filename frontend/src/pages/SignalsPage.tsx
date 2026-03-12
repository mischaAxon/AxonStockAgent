import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Search, Zap, ChevronDown, ChevronUp, AlertTriangle, XCircle, Clock } from 'lucide-react';
import { useSignals, useProviders } from '../hooks/useApi';
import type { Signal } from '../types';
import { relativeTime } from '../utils/formatTime';
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';

type VerdictFilter = '' | 'BUY' | 'SELL' | 'SQUEEZE';
type Period = 'today' | 'week' | 'month' | 'all';

function sinceISO(period: Period): string | undefined {
  const now = new Date();
  if (period === 'today') { const d = new Date(now); d.setHours(0, 0, 0, 0); return d.toISOString(); }
  if (period === 'week')  { const d = new Date(now); d.setDate(d.getDate() - 7); return d.toISOString(); }
  if (period === 'month') { const d = new Date(now); d.setMonth(d.getMonth() - 1); return d.toISOString(); }
  return undefined;
}

// ── Provider warnings ─────────────────────────────────────────────────────────

interface ProviderInfo {
  name: string;
  isEnabled: boolean;
  healthStatus: string;
}

function ProviderWarningBanner() {
  const { data } = useProviders();
  const providers: ProviderInfo[] = (data as { data?: ProviderInfo[] } | undefined)?.data ?? [];

  const finnhub = providers.find(p => p.name === 'finnhub');
  const eodhd   = providers.find(p => p.name === 'eodhd');

  const finnhubDown    = finnhub?.isEnabled && (finnhub.healthStatus === 'down' || finnhub.healthStatus === 'degraded');
  const noActiveEodhd  = !eodhd?.isEnabled;
  const finnhubOnlyActive = finnhub?.isEnabled && noActiveEodhd;

  if (!finnhubDown && !finnhubOnlyActive) return null;

  return (
    <div className="mb-4 space-y-2">
      {finnhubDown && (
        <div className="flex items-start gap-3 rounded-lg bg-red-500/10 border border-red-500/30 px-4 py-3 text-sm text-red-400">
          <XCircle size={15} className="mt-0.5 flex-shrink-0" />
          <div>
            <span className="font-semibold">Finnhub is niet bereikbaar.</span>
            {' '}Signaalgeneratie kan mislukken. Controleer de status op de{' '}
            <a href="/admin/providers" className="underline hover:text-red-300 transition-colors">Providers-pagina</a>.
          </div>
        </div>
      )}

      {finnhubOnlyActive && !finnhubDown && (
        <div className="flex items-start gap-3 rounded-lg bg-amber-500/10 border border-amber-500/30 px-4 py-3 text-sm text-amber-400">
          <AlertTriangle size={15} className="mt-0.5 flex-shrink-0" />
          <div>
            <span className="font-semibold">Geen actieve candle-provider.</span>
            {' '}Finnhub gratis tier blokkeert historische OHLCV-data. Signaalgeneratie vereist EODHD (EOD-candles, dagelijks bijgewerkt na marktsluit). Voeg EODHD toe op de{' '}
            <a href="/admin/providers" className="underline hover:text-amber-300 transition-colors">Providers-pagina</a>.
          </div>
        </div>
      )}
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

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

function SignalDetail({ signal }: { signal: Signal }) {
  const techNorm   = ((signal.techScore + 1) / 2) * 100;
  const sentNorm   = signal.sentimentScore !== null ? ((signal.sentimentScore + 1) / 2) * 100 : null;
  const claudeScore = signal.claudeConfidence !== null ? signal.claudeConfidence * 100 : null;
  const claudeLabel = `Claude AI${signal.claudeDirection ? ` (${signal.claudeDirection})` : ''}`;
  const mlScore    = signal.mlProbability !== null ? signal.mlProbability * 100 : null;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
      {/* Score breakdown */}
      <div>
        <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Score Breakdown</p>
        <div className="space-y-2.5">
          <BreakdownBar label="Technical"    score={techNorm}    tooltip={TOOLTIPS.techScore} />
          <BreakdownBar label="Sentiment"    score={sentNorm}    tooltip={TOOLTIPS.sentimentScore} />
          <BreakdownBar label={claudeLabel}  score={claudeScore} tooltip={TOOLTIPS.claudeAI} />
          <BreakdownBar label="ML"           score={mlScore}     tooltip={TOOLTIPS.mlScore} />
          <BreakdownBar label="Fundamentals" score={null}        tooltip={TOOLTIPS.fundamentalsScore} />
        </div>
      </div>

      {/* Status badges + Claude reasoning */}
      <div>
        <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Status Indicatoren</p>
        <div className="flex flex-wrap gap-2 mb-4">
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
        </div>

        {signal.claudeReasoning && (
          <>
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Claude Reasoning</p>
            <p className="text-sm text-gray-300 leading-relaxed">{signal.claudeReasoning}</p>
          </>
        )}
      </div>
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

// ── Main page ─────────────────────────────────────────────────────────────────

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
      <ProviderWarningBanner />

      {/* Header row */}
      <div className="flex flex-wrap items-center justify-between gap-4 mb-4">
        <div className="flex items-center gap-3">
          <h2 className="text-2xl font-bold text-white">Signalen</h2>
          {data?.meta && (
            <span className="px-2.5 py-0.5 rounded-full bg-gray-800 text-gray-400 text-sm font-medium">
              {data.meta.total} totaal
            </span>
          )}
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-blue-500/10 text-blue-400 text-xs font-medium">
            <Clock size={11} />
            EOD · dagelijks 22:30 UTC
          </span>
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
