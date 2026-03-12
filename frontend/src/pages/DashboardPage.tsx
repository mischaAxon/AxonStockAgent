import React from 'react';
import { TrendingUp, TrendingDown, Zap, Eye, Briefcase, Activity } from 'lucide-react';
import { useDashboard, useSectorSentiment, useTrending } from '../hooks/useApi';
import type { Signal, SectorSentiment, TrendingSymbol } from '../types';
import { relativeTime } from '../utils/formatTime';
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
import { TOOLTIPS } from '../utils/tooltipTexts';

// ── Sub-components ────────────────────────────────────────────────────────────

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

function ScannerStatus({ createdAt }: { createdAt: string | undefined }) {
  const isActive = createdAt
    ? Date.now() - new Date(createdAt).getTime() < 30 * 60 * 1000
    : false;

  return (
    <div className="flex items-center gap-2">
      <span className={`inline-block w-2 h-2 rounded-full ${isActive ? 'bg-green-400' : 'bg-gray-600'}`} />
      <span className={`text-sm ${isActive ? 'text-green-400' : 'text-gray-500'}`}>
        {isActive ? 'Scanner actief' : 'Geen recente scans'}
      </span>
    </div>
  );
}

function SectorSentimentWidget({ data }: { data: SectorSentiment[] }) {
  const sorted = [...data].sort((a, b) => b.avgSentiment - a.avgSentiment);

  if (sorted.length === 0) {
    return <p className="text-gray-500 text-sm">Geen sector sentiment beschikbaar.</p>;
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
      {sorted.map((s) => {
        const pct = Math.round(((s.avgSentiment + 1) / 2) * 100);
        const barColor = s.avgSentiment > 0.1 ? 'bg-green-500' : s.avgSentiment < -0.1 ? 'bg-red-500' : 'bg-gray-500';
        const textColor = s.avgSentiment > 0.1 ? 'text-green-400' : s.avgSentiment < -0.1 ? 'text-red-400' : 'text-gray-400';

        return (
          <div key={s.sector} className="bg-gray-800/50 rounded-lg px-4 py-3">
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium text-white">{s.sector}</span>
              <span className={`font-mono text-xs ${textColor}`}>
                {s.avgSentiment > 0 ? '+' : ''}{s.avgSentiment.toFixed(2)}
              </span>
            </div>
            <div className="h-1 bg-gray-700 rounded-full overflow-hidden mb-1.5">
              <div className={`h-full rounded-full ${barColor}`} style={{ width: `${pct}%` }} />
            </div>
            <p className="text-xs text-gray-500">{s.articleCount} artikelen</p>
          </div>
        );
      })}
    </div>
  );
}

function TrendingWidget({ data }: { data: TrendingSymbol[] }) {
  if (data.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-2">
      {data.map((t) => {
        const dot = t.avgSentiment > 0.1 ? 'bg-green-400' : t.avgSentiment < -0.1 ? 'bg-red-400' : 'bg-gray-500';
        return (
          <span
            key={t.symbol}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-gray-800 border border-gray-700 rounded-full text-sm font-mono font-semibold text-white"
          >
            <span className={`w-1.5 h-1.5 rounded-full ${dot}`} />
            {t.symbol}
            <span className="text-gray-500 font-normal font-sans text-xs">{t.articleCount}</span>
          </span>
        );
      })}
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { data, isLoading, error } = useDashboard();
  const { data: sectorData }       = useSectorSentiment();
  const { data: trendingData }     = useTrending();

  if (isLoading) return <div className="text-gray-400">Laden...</div>;
  if (error)     return <div className="text-red-400">Fout bij laden dashboard</div>;

  const d = data?.data;
  if (!d) return null;

  const sectorSentiments: SectorSentiment[] = Array.isArray(sectorData)   ? sectorData   : [];
  const trendingSymbols: TrendingSymbol[]   = Array.isArray(trendingData) ? trendingData : [];
  const latestSignalAt = d.recentSignals[0]?.createdAt;

  return (
    <div>
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-white">Dashboard</h2>
        <ScannerStatus createdAt={latestSignalAt} />
      </div>

      {/* 5 stat cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 mb-8">
        <StatCard icon={<Eye size={20} />}          label="Watchlist"      value={d.watchlistCount}       color="blue"   tooltip={TOOLTIPS.watchlistCount} />
        <StatCard icon={<Briefcase size={20} />}    label="Portfolio"      value={d.portfolioPositions}   color="purple" tooltip={TOOLTIPS.portfolioPositions} />
        <StatCard icon={<TrendingUp size={20} />}   label="BUY deze week"  value={d.signals.weekBuys}     color="green"  tooltip={TOOLTIPS.weekBuys} />
        <StatCard icon={<TrendingDown size={20} />} label="SELL deze week" value={d.signals.weekSells}    color="red"    tooltip={TOOLTIPS.weekSells} />
        <StatCard icon={<Zap size={20} />}          label="SQUEEZE"        value={d.signals.weekSqueezes} color="amber"  tooltip={TOOLTIPS.weekSqueezes} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
        {/* Recent signals (2/3) */}
        <div className="lg:col-span-2 bg-gray-900 rounded-xl border border-gray-800 p-6">
          <h3 className="text-lg font-semibold text-white mb-4">Recente Signalen</h3>
          {d.recentSignals.length === 0 ? (
            <p className="text-gray-500 text-sm">Nog geen signalen. De scanner draait op de achtergrond.</p>
          ) : (
            <div className="space-y-3">
              {d.recentSignals.slice(0, 5).map((signal: Signal) => (
                <div key={signal.id} className="flex items-center justify-between bg-gray-800/50 rounded-lg px-4 py-3">
                  <div className="flex items-center gap-3">
                    <VerdictBadge verdict={signal.finalVerdict} />
                    <div>
                      <span className="font-mono font-semibold text-white">{signal.symbol}</span>
                      <div className="mt-1"><ScoreBar score={signal.finalScore} width="w-20" /></div>
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-medium text-white">€{signal.priceAtSignal?.toFixed(2)}</div>
                    <div className="text-xs text-gray-500">{relativeTime(signal.createdAt)}</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Trending (1/3) */}
        {trendingSymbols.length > 0 && (
          <div className="bg-gray-900 rounded-xl border border-gray-800 p-6">
            <div className="flex items-center gap-2 mb-4">
              <Activity size={16} className="text-gray-400" />
              <h3 className="text-lg font-semibold text-white flex items-center gap-2">Trending<InfoTooltip text={TOOLTIPS.trending} /></h3>
            </div>
            <TrendingWidget data={trendingSymbols} />
          </div>
        )}
      </div>

      {/* Sector sentiment */}
      <div className="bg-gray-900 rounded-xl border border-gray-800 p-6">
        <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">Sector Sentiment<InfoTooltip text={TOOLTIPS.sectorSentiment} /></h3>
        <SectorSentimentWidget data={sectorSentiments} />
      </div>
    </div>
  );
}
