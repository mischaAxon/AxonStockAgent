import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, ReferenceLine } from 'recharts';
import { useFundamentals, useInsiderTransactions, useAllSymbols, useSignals, useNewsBySymbol, useBatchQuotes, useSentimentChanges } from '../hooks/useApi';
import type { CompanyFundamentals, InsiderTransaction, Signal, NewsArticle, MarketSymbol } from '../types';
import { relativeTime } from '../utils/formatTime';
import { VerdictBadge, ScoreBar, InfoTooltip } from '../components/shared';
import PillarScoreBar from '../components/PillarScoreBar';
import { TOOLTIPS } from '../utils/tooltipTexts';

// ─── helpers ────────────────────────────────────────────────────────────────

function fmt(val: number | null | undefined, decimals = 2): string {
  if (val == null) return '—';
  return val.toFixed(decimals);
}

function fmtPct(val: number | null | undefined): string {
  if (val == null) return '—';
  return (val * 100).toFixed(1) + '%';
}

function fmtLarge(val: number | null | undefined): string {
  if (val == null) return '—';
  if (Math.abs(val) >= 1e12) return (val / 1e12).toFixed(2) + 'T';
  if (Math.abs(val) >= 1e9)  return (val / 1e9).toFixed(2)  + 'B';
  if (Math.abs(val) >= 1e6)  return (val / 1e6).toFixed(2)  + 'M';
  return val.toFixed(0);
}

function relativeDate(iso: string) {
  const d = new Date(iso);
  const diff = (Date.now() - d.getTime()) / 1000;
  if (diff < 3600) return Math.floor(diff / 60) + 'm geleden';
  if (diff < 86400) return Math.floor(diff / 3600) + 'u geleden';
  return d.toLocaleDateString('nl-NL');
}

function countryFlag(code: string | null): string {
  if (!code || code.length !== 2) return '';
  return code.toUpperCase().replace(/./g, c =>
    String.fromCodePoint(c.charCodeAt(0) + 127397));
}

// ─── colour helpers ──────────────────────────────────────────────────────────

function peColor(v: number | null) {
  if (v == null) return 'text-gray-400';
  if (v < 15) return 'text-green-400';
  if (v < 25) return 'text-yellow-400';
  return 'text-red-400';
}

function pctColor(v: number | null) {
  if (v == null) return 'text-gray-400';
  return v >= 0 ? 'text-green-400' : 'text-red-400';
}

function deColor(v: number | null) {
  if (v == null) return 'text-gray-400';
  if (v < 1) return 'text-green-400';
  if (v < 2) return 'text-yellow-400';
  return 'text-red-400';
}

function ratioColor(v: number | null) {
  if (v == null) return 'text-gray-400';
  if (v > 1.5) return 'text-green-400';
  if (v >= 1)  return 'text-yellow-400';
  return 'text-red-400';
}

function payoutColor(v: number | null) {
  if (v == null) return 'text-gray-400';
  if (v < 0.6) return 'text-green-400';
  if (v < 0.8) return 'text-yellow-400';
  return 'text-red-400';
}

// ─── sub-components ──────────────────────────────────────────────────────────

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

function Skeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-8 bg-gray-800 rounded w-48" />
      <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-20 bg-gray-800 rounded-xl" />
        ))}
      </div>
    </div>
  );
}

function ChartTooltip({ active, payload }: { active?: boolean; payload?: Array<{ payload: { date: string; score: number; verdict: string } }> }) {
  if (!active || !payload?.[0]) return null;
  const d = payload[0].payload;
  return (
    <div className="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-xs">
      <p className="text-gray-400">{d.date}</p>
      <p className="text-white font-semibold">{(d.score * 100).toFixed(1)}%</p>
      <p className={d.verdict === 'BUY' ? 'text-green-400' : d.verdict === 'SELL' ? 'text-red-400' : 'text-amber-400'}>
        {d.verdict}
      </p>
    </div>
  );
}

function ChartDot(props: { cx?: number; cy?: number; payload?: { verdict: string } }) {
  const { cx, cy, payload } = props;
  if (cx == null || cy == null || !payload) return null;
  const color = payload.verdict === 'BUY' ? '#22c55e' : payload.verdict === 'SELL' ? '#ef4444' : '#f59e0b';
  return <circle cx={cx} cy={cy} r={4} fill={color} stroke="none" />;
}

function AnalystBar({ data }: { data: CompanyFundamentals }) {
  const sb = data.analystStrongBuy  ?? 0;
  const b  = data.analystBuy        ?? 0;
  const h  = data.analystHold       ?? 0;
  const s  = data.analystSell       ?? 0;
  const ss = data.analystStrongSell ?? 0;
  const total = sb + b + h + s + ss;
  if (total === 0) return <p className="text-gray-500 text-sm">Geen analistendata</p>;

  const pct = (n: number) => ((n / total) * 100).toFixed(0) + '%';

  return (
    <div className="space-y-3">
      <div className="flex rounded-lg overflow-hidden h-7 text-xs font-semibold">
        {sb > 0 && <div className="bg-green-700 flex items-center justify-center" style={{ width: pct(sb) }}>{sb}</div>}
        {b  > 0 && <div className="bg-green-500 flex items-center justify-center" style={{ width: pct(b)  }}>{b}</div>}
        {h  > 0 && <div className="bg-yellow-500 flex items-center justify-center text-gray-900" style={{ width: pct(h)  }}>{h}</div>}
        {s  > 0 && <div className="bg-orange-500 flex items-center justify-center" style={{ width: pct(s)  }}>{s}</div>}
        {ss > 0 && <div className="bg-red-600 flex items-center justify-center"    style={{ width: pct(ss) }}>{ss}</div>}
      </div>
      <div className="flex gap-3 text-xs text-gray-400 flex-wrap">
        <span><span className="inline-block w-2 h-2 bg-green-700 rounded-sm mr-1" />Strong Buy: {sb}</span>
        <span><span className="inline-block w-2 h-2 bg-green-500 rounded-sm mr-1" />Buy: {b}</span>
        <span><span className="inline-block w-2 h-2 bg-yellow-500 rounded-sm mr-1" />Hold: {h}</span>
        <span><span className="inline-block w-2 h-2 bg-orange-500 rounded-sm mr-1" />Sell: {s}</span>
        <span><span className="inline-block w-2 h-2 bg-red-600 rounded-sm mr-1" />Strong Sell: {ss}</span>
      </div>

      {(data.targetPriceMean != null) && (
        <div className="mt-2 space-y-1">
          <p className="text-xs text-gray-500 flex items-center">Koersdoel analisten<InfoTooltip text={TOOLTIPS.targetPrice} size={11} /></p>
          <div className="flex items-center gap-2 text-sm">
            <span className="text-gray-400">Laag: <span className="text-white">${fmt(data.targetPriceLow)}</span></span>
            <span className="text-gray-600">·</span>
            <span className="text-axon-400 font-semibold">Gem: ${fmt(data.targetPriceMean)}</span>
            <span className="text-gray-600">·</span>
            <span className="text-gray-400">Mediaan: <span className="text-white">${fmt(data.targetPriceMedian)}</span></span>
            <span className="text-gray-600">·</span>
            <span className="text-gray-400">Hoog: <span className="text-white">${fmt(data.targetPriceHigh)}</span></span>
          </div>
        </div>
      )}
    </div>
  );
}

function InsiderTable({ transactions }: { transactions: InsiderTransaction[] }) {
  const [showAll, setShowAll] = useState(false);
  const visible = showAll ? transactions : transactions.slice(0, 20);

  if (transactions.length === 0) {
    return <p className="text-gray-500 text-sm">Geen insider transacties</p>;
  }

  return (
    <div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-xs text-gray-500 border-b border-gray-800">
              <th className="text-left py-2 pr-4">Datum</th>
              <th className="text-left py-2 pr-4">Naam</th>
              <th className="text-left py-2 pr-4">Rol</th>
              <th className="text-left py-2 pr-4">Type</th>
              <th className="text-right py-2 pr-4">Aandelen</th>
              <th className="text-right py-2 pr-4">Prijs</th>
              <th className="text-right py-2">Totaal</th>
            </tr>
          </thead>
          <tbody>
            {visible.map(tx => (
              <tr key={tx.id} className="border-b border-gray-800/50 hover:bg-gray-800/30">
                <td className="py-2 pr-4 text-gray-400 whitespace-nowrap">{new Date(tx.transactionDate).toLocaleDateString('nl-NL')}</td>
                <td className="py-2 pr-4 text-white">{tx.name}</td>
                <td className="py-2 pr-4 text-gray-400 text-xs">{tx.relation}</td>
                <td className="py-2 pr-4">
                  <span className={`px-2 py-0.5 rounded text-xs font-medium ${
                    tx.transactionType.toLowerCase().includes('buy')
                      ? 'bg-green-500/15 text-green-400'
                      : 'bg-red-500/15 text-red-400'
                  }`}>
                    {tx.transactionType}
                  </span>
                </td>
                <td className="py-2 pr-4 text-right text-gray-300">{tx.shares.toLocaleString()}</td>
                <td className="py-2 pr-4 text-right text-gray-300">${fmt(tx.pricePerShare)}</td>
                <td className="py-2 text-right text-gray-300">${fmtLarge(tx.totalValue)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {transactions.length > 20 && !showAll && (
        <button
          onClick={() => setShowAll(true)}
          className="mt-3 text-xs text-axon-400 hover:text-axon-300"
        >
          Toon meer ({transactions.length - 20} meer)
        </button>
      )}
    </div>
  );
}

function NewsTab({ news }: { news: NewsArticle[] }) {
  const [showAll, setShowAll] = useState(false);
  const visible = showAll ? news : news.slice(0, 10);

  if (news.length === 0) {
    return (
      <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 text-center">
        <p className="text-gray-500 text-sm">Geen recent nieuws beschikbaar.</p>
      </div>
    );
  }

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl divide-y divide-gray-800">
      {visible.map((article) => {
        const sentDot = article.sentimentScore > 0.1
          ? 'bg-green-400'
          : article.sentimentScore < -0.1
            ? 'bg-red-400'
            : 'bg-gray-500';

        return (
          <div key={article.id} className="px-4 py-3 flex items-start gap-3">
            <span className={`w-2 h-2 rounded-full mt-1.5 shrink-0 ${sentDot}`} />
            <div className="min-w-0 flex-1">
              {article.url ? (
                <a
                  href={article.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm text-white hover:text-axon-300 transition-colors line-clamp-2"
                >
                  {article.headline}
                </a>
              ) : (
                <p className="text-sm text-white line-clamp-2">{article.headline}</p>
              )}
              <div className="flex items-center gap-2 mt-1">
                <span className="text-xs text-gray-500">{article.source}</span>
                <span className="text-gray-700">·</span>
                <span className="text-xs text-gray-500">{relativeTime(article.publishedAt)}</span>
                <span className="text-gray-700">·</span>
                <span className={`text-xs font-mono ${
                  article.sentimentScore > 0.1 ? 'text-green-400'
                    : article.sentimentScore < -0.1 ? 'text-red-400'
                    : 'text-gray-500'
                }`}>
                  {article.sentimentScore > 0 ? '+' : ''}{(article.sentimentScore * 100).toFixed(0)}%
                </span>
              </div>
            </div>
          </div>
        );
      })}
      {news.length > 10 && !showAll && (
        <button
          onClick={() => setShowAll(true)}
          className="w-full px-4 py-3 text-xs text-axon-400 hover:text-axon-300 hover:bg-gray-800/30 transition-colors"
        >
          Toon alle {news.length} artikelen →
        </button>
      )}
    </div>
  );
}

function ProfileTab({ watchlistItem, fund }: { watchlistItem: MarketSymbol | undefined; fund: CompanyFundamentals | undefined }) {
  return (
    <div className="space-y-6">
      <section>
        <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Bedrijfsinformatie</h2>
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-5">
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            <div>
              <p className="text-xs text-gray-500 mb-1">Sector</p>
              <p className="text-sm text-white">{watchlistItem?.sector ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Industrie</p>
              <p className="text-sm text-white">{watchlistItem?.industry ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Land</p>
              <p className="text-sm text-white">
                {watchlistItem?.country ? `${countryFlag(watchlistItem.country)} ${watchlistItem.country}` : '—'}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Beurs</p>
              <p className="text-sm text-white">{watchlistItem?.exchange ?? '—'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Marktkapitalisatie</p>
              <p className="text-sm text-white">
                {fund?.marketCap != null ? '$' + fmtLarge(fund.marketCap) : watchlistItem?.marketCap != null ? '$' + fmtLarge(watchlistItem.marketCap) : '—'}
              </p>
            </div>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">AI Analyse</h2>
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 text-center">
          <p className="text-gray-500 text-sm">AI-gegenereerde bedrijfssamenvatting komt in een toekomstige update.</p>
          <p className="text-xs text-gray-600 mt-1">
            Denk aan: business overview, seizoenaliteit, concurrentiepositie, en risicofactoren — vergelijkbaar met EdgeHound's "General" sectie.
          </p>
        </div>
      </section>
    </div>
  );
}

// ─── main page ───────────────────────────────────────────────────────────────

const tabs = ['Signalen', 'Nieuws', 'Fundamentals', 'Profiel'] as const;
type Tab = typeof tabs[number];

export default function StockDetailPage() {
  const { symbol = '' } = useParams<{ symbol: string }>();
  const upperSymbol = symbol.toUpperCase();
  const [activeTab, setActiveTab] = useState<Tab>('Signalen');

  const { data: allSymbolsData }   = useAllSymbols();
  const { data: fundData, isLoading: fundLoading, error: fundError, refetch } = useFundamentals(upperSymbol);
  const { data: insidersData, isLoading: insidersLoading } = useInsiderTransactions(upperSymbol);
  const { data: signalsData,  isLoading: signalsLoading }  = useSignals(1, 10, upperSymbol);
  const { data: newsData }        = useNewsBySymbol(upperSymbol);
  const { data: quoteData }       = useBatchQuotes([upperSymbol]);
  const quote = quoteData?.data?.[upperSymbol];
  const { data: sentimentData }   = useSentimentChanges(7);
  const sentimentChange = sentimentData?.data?.find(s => s.symbol === upperSymbol);

  const watchlistItem = allSymbolsData?.data?.find(m => m.symbol === upperSymbol);
  const fund: CompanyFundamentals | undefined = fundData?.data;
  const insiders: InsiderTransaction[] = insidersData?.data ?? [];
  const signals: Signal[]   = signalsData?.data ?? [];
  const news: NewsArticle[] = Array.isArray(newsData) ? newsData : [];

  const latestSignal = signals[0];
  const totalSignals = signalsData?.meta?.total ?? 0;
  const avgScore = signals.length > 0
    ? signals.reduce((acc, s) => acc + s.finalScore, 0) / signals.length
    : null;

  const chartData = [...signals]
    .reverse()
    .map(s => ({
      date: new Date(s.createdAt).toLocaleDateString('nl-NL', { day: '2-digit', month: '2-digit' }),
      score: s.finalScore,
      verdict: s.finalVerdict,
    }));

  return (
    <div className="space-y-6">
      {/* Back link */}
      <Link to="/" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white transition-colors">
        <ArrowLeft size={14} /> Terug naar Markets
      </Link>

      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          {watchlistItem?.logo ? (
            <img
              src={watchlistItem.logo}
              alt={upperSymbol}
              className="w-12 h-12 rounded-xl object-contain bg-white/5 p-1"
              onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
            />
          ) : (
            <div className="w-12 h-12 rounded-xl bg-gray-800 flex items-center justify-center">
              <span className="font-mono text-sm text-gray-400">{upperSymbol.slice(0, 3)}</span>
            </div>
          )}
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold text-white font-mono">{upperSymbol}</h1>
              {watchlistItem?.country && (
                <span className="text-xl">{countryFlag(watchlistItem.country)}</span>
              )}
            </div>
            {watchlistItem?.name && <p className="text-gray-400 text-sm">{watchlistItem.name}</p>}
            <div className="flex flex-wrap gap-1.5 mt-1">
              {watchlistItem?.sector && (
                <span className="px-2 py-0.5 rounded text-xs bg-axon-600/20 text-axon-400">{watchlistItem.sector}</span>
              )}
              {watchlistItem?.industry && (
                <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-400">{watchlistItem.industry}</span>
              )}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          {fund && (
            <p className="text-xs text-gray-500">Bijgewerkt: {relativeDate(fund.updatedAt)}</p>
          )}
          <button
            onClick={() => refetch()}
            className="p-2 rounded-lg bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-white transition-colors"
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

      {/* ── Live Price ──────────────────────────────────────────────────────── */}
      {quote && (
        <div className="flex items-center gap-4 bg-gray-900 border border-gray-800 rounded-xl px-5 py-3">
          <div>
            <p className="text-xs text-gray-500 mb-0.5 flex items-center gap-1">
              Live Prijs<InfoTooltip text={TOOLTIPS.livePrice} size={11} />
              {quote.timestamp && (() => {
                const ageMin = Math.floor((Date.now() - new Date(quote.timestamp).getTime()) / 60000);
                const isStale = ageMin > 15;
                return (
                  <span className={`text-[10px] ml-1 ${isStale ? 'text-orange-400' : 'text-gray-600'}`}>
                    {ageMin < 1 ? 'zojuist' : `${ageMin}m geleden`}{isStale ? ' ⚠' : ''}
                  </span>
                );
              })()}
            </p>
            <p className="text-3xl font-bold font-mono text-white">
              {quote.currentPrice >= 1000
                ? quote.currentPrice.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                : quote.currentPrice.toFixed(2)}
            </p>
          </div>
          <div className={`flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm font-semibold ${
            quote.changePercent > 0
              ? 'bg-green-500/15 text-green-400'
              : quote.changePercent < 0
                ? 'bg-red-500/15 text-red-400'
                : 'bg-gray-800 text-gray-400'
          }`}>
            {quote.changePercent > 0 ? '▲' : quote.changePercent < 0 ? '▼' : '—'}
            {' '}
            {quote.changePercent > 0 ? '+' : ''}{quote.changePercent.toFixed(2)}%
            <span className="text-xs opacity-70 ml-1">
              ({quote.change > 0 ? '+' : ''}{quote.change.toFixed(2)})
            </span>
          </div>
          <div className="ml-auto flex gap-6 text-xs text-gray-500">
            <div>
              <span className="flex items-center text-gray-600">Open<InfoTooltip text={TOOLTIPS.open} size={11} /></span>
              <span className="text-gray-300 font-mono">{quote.open.toFixed(2)}</span>
            </div>
            <div>
              <span className="flex items-center text-gray-600">High<InfoTooltip text={TOOLTIPS.high} size={11} /></span>
              <span className="text-green-400/70 font-mono">{quote.high.toFixed(2)}</span>
            </div>
            <div>
              <span className="flex items-center text-gray-600">Low<InfoTooltip text={TOOLTIPS.low} size={11} /></span>
              <span className="text-red-400/70 font-mono">{quote.low.toFixed(2)}</span>
            </div>
            <div>
              <span className="flex items-center text-gray-600">Prev Close<InfoTooltip text={TOOLTIPS.prevClose} size={11} /></span>
              <span className="text-gray-300 font-mono">{quote.previousClose.toFixed(2)}</span>
            </div>
            <div>
              <span className="flex items-center text-gray-600">Volume<InfoTooltip text={TOOLTIPS.volume} size={11} /></span>
              <span className="text-gray-300 font-mono">
                {quote.volume >= 1e6
                  ? (quote.volume / 1e6).toFixed(1) + 'M'
                  : quote.volume >= 1e3
                    ? (quote.volume / 1e3).toFixed(0) + 'K'
                    : quote.volume.toLocaleString()}
              </span>
            </div>
          </div>
        </div>
      )}

      {/* ── Tab Navigation ─────────────────────────────────────────────────── */}
      <div className="flex gap-1 border-b border-gray-800">
        {tabs.map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-4 py-2.5 text-sm font-medium transition-colors relative ${
              activeTab === tab
                ? 'text-axon-400'
                : 'text-gray-500 hover:text-gray-300'
            }`}
          >
            {tab}
            {activeTab === tab && (
              <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-axon-500 rounded-full" />
            )}
          </button>
        ))}
      </div>

      {/* ── Tab Content ────────────────────────────────────────────────────── */}

      {activeTab === 'Signalen' && (
        <div className="space-y-6">
          {/* Quick Stats Banner */}
          <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-1.5 flex items-center">Laatste Signaal<InfoTooltip text={TOOLTIPS.verdict} /></p>
              {signalsLoading ? (
                <div className="h-5 bg-gray-800 rounded animate-pulse w-16" />
              ) : latestSignal ? (
                <div>
                  <VerdictBadge verdict={latestSignal.finalVerdict} />
                  <p className="text-xs text-gray-500 mt-1">{relativeTime(latestSignal.createdAt)}</p>
                </div>
              ) : (
                <p className="text-gray-600 text-sm">Geen signalen</p>
              )}
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-1.5 flex items-center">Score<InfoTooltip text={TOOLTIPS.finalScore} /></p>
              {signalsLoading ? (
                <div className="h-5 bg-gray-800 rounded animate-pulse w-12" />
              ) : latestSignal ? (
                <p className={`text-xl font-bold font-mono ${
                  latestSignal.finalScore >= 0.6 ? 'text-green-400'
                  : latestSignal.finalScore >= 0.3 ? 'text-amber-400'
                  : 'text-red-400'
                }`}>
                  {(latestSignal.finalScore * 100).toFixed(1)}%
                </p>
              ) : (
                <p className="text-gray-600 text-xl font-bold">—</p>
              )}
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-1.5 flex items-center">Totaal Signalen<InfoTooltip text={TOOLTIPS.totalSignals} /></p>
              {signalsLoading ? (
                <div className="h-5 bg-gray-800 rounded animate-pulse w-8" />
              ) : (
                <p className="text-xl font-bold text-white">{totalSignals}</p>
              )}
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-1.5 flex items-center">Gemiddelde Score<InfoTooltip text={TOOLTIPS.avgScore} /></p>
              {signalsLoading ? (
                <div className="h-5 bg-gray-800 rounded animate-pulse w-12" />
              ) : avgScore != null ? (
                <p className={`text-xl font-bold font-mono ${
                  avgScore >= 0.6 ? 'text-green-400' : avgScore >= 0.3 ? 'text-amber-400' : 'text-red-400'
                }`}>
                  {(avgScore * 100).toFixed(1)}%
                </p>
              ) : (
                <p className="text-gray-600 text-xl font-bold">—</p>
              )}
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-2 flex items-center">Score Breakdown<InfoTooltip text="Hoe elke pijler bijdraagt aan de totaalscore: Tech (technische indicatoren), Fund (fundamentals), Sent (sentiment), AI (Claude analyse)." /></p>
              {signalsLoading ? (
                <div className="h-16 bg-gray-800 rounded animate-pulse" />
              ) : latestSignal ? (
                <PillarScoreBar
                  techScore={latestSignal.techScore}
                  sentimentScore={latestSignal.sentimentScore}
                  claudeConfidence={latestSignal.claudeConfidence}
                  fundamentalsScore={latestSignal.fundamentalsScore}
                />
              ) : (
                <p className="text-gray-600 text-sm">Geen data</p>
              )}
            </div>

            {/* Sentiment Δ */}
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <p className="text-xs text-gray-500 mb-1.5 flex items-center">
                Sentiment Δ
                <InfoTooltip text="Verandering in sentimentscore over de afgelopen 7 dagen. Verschil tussen recent en eerder gemiddeld sentiment." />
              </p>
              {sentimentChange ? (
                <div>
                  <p className={`text-xl font-bold font-mono ${
                    sentimentChange.sentimentChange! > 0 ? 'text-green-400'
                    : sentimentChange.sentimentChange! < 0 ? 'text-red-400'
                    : 'text-gray-400'
                  }`}>
                    {sentimentChange.sentimentChange! > 0 ? '+' : ''}{sentimentChange.sentimentChange}%
                  </p>
                  {sentimentChange.currentSentiment != null && (
                    <p className="text-[10px] text-gray-500 mt-0.5">
                      Huidig: {sentimentChange.currentSentiment}%
                    </p>
                  )}
                </div>
              ) : (
                <p className="text-gray-600 text-xl font-bold">—</p>
              )}
            </div>
          </div>

          {/* Score Trend Chart */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Score Trend<InfoTooltip text={TOOLTIPS.scoreTrend} /></h2>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              {signalsLoading ? (
                <div className="h-[200px] bg-gray-800 rounded animate-pulse" />
              ) : chartData.length < 2 ? (
                <p className="text-gray-500 text-sm py-8 text-center">
                  Niet genoeg data voor een trend chart.
                </p>
              ) : (
                <ResponsiveContainer width="100%" height={200}>
                  <LineChart data={chartData} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                    <XAxis
                      dataKey="date"
                      tick={{ fontSize: 10, fill: '#6b7280' }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <YAxis
                      domain={[0, 1]}
                      tickFormatter={(v: number) => `${Math.round(v * 100)}%`}
                      tick={{ fontSize: 10, fill: '#6b7280' }}
                      axisLine={false}
                      tickLine={false}
                      width={36}
                    />
                    <Tooltip content={<ChartTooltip />} />
                    <ReferenceLine y={0.65} stroke="#22c55e" strokeDasharray="4 3" strokeOpacity={0.5} />
                    <ReferenceLine y={0.35} stroke="#ef4444" strokeDasharray="4 3" strokeOpacity={0.5} />
                    <Line
                      type="monotone"
                      dataKey="score"
                      stroke="#3b82f6"
                      strokeWidth={2}
                      dot={<ChartDot />}
                      activeDot={{ r: 5, fill: '#3b82f6' }}
                    />
                  </LineChart>
                </ResponsiveContainer>
              )}
            </div>
          </section>

          {/* Signaalhistorie */}
          <section>
            <div className="flex items-center gap-2 mb-3">
              <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Signaalhistorie</h2>
              {totalSignals > 0 && (
                <span className="px-2 py-0.5 rounded-full bg-gray-800 text-gray-400 text-xs">{totalSignals}</span>
              )}
            </div>
            <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
              {signalsLoading ? (
                <div className="p-4 animate-pulse space-y-2">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <div key={i} className="h-8 bg-gray-800 rounded" />
                  ))}
                </div>
              ) : signals.length === 0 ? (
                <p className="text-gray-500 text-sm p-4">Nog geen signalen voor {upperSymbol}.</p>
              ) : (
                <>
                  <table className="w-full text-sm">
                    <thead className="bg-gray-800/50">
                      <tr>
                        <th className="px-4 py-2.5 text-left text-xs text-gray-400 font-medium">Datum</th>
                        <th className="px-4 py-2.5 text-left text-xs text-gray-400 font-medium">Verdict</th>
                        <th className="px-4 py-2.5 text-left text-xs text-gray-400 font-medium w-36">Score</th>
                        <th className="px-4 py-2.5 text-right text-xs text-gray-400 font-medium">Prijs</th>
                        <th className="px-4 py-2.5 text-left text-xs text-gray-400 font-medium hidden md:table-cell">Claude</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-800">
                      {signals.map((s) => (
                        <tr key={s.id} className="hover:bg-gray-800/30">
                          <td className="px-4 py-2.5 text-gray-400 whitespace-nowrap text-xs">{relativeTime(s.createdAt)}</td>
                          <td className="px-4 py-2.5"><VerdictBadge verdict={s.finalVerdict} /></td>
                          <td className="px-4 py-2.5"><ScoreBar score={s.finalScore} width="w-16" /></td>
                          <td className="px-4 py-2.5 text-right font-mono text-white text-xs">€{s.priceAtSignal.toFixed(2)}</td>
                          <td className="px-4 py-2.5 text-gray-500 text-xs hidden md:table-cell max-w-xs truncate">
                            {s.claudeReasoning
                              ? s.claudeReasoning.slice(0, 60) + (s.claudeReasoning.length > 60 ? '…' : '')
                              : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {totalSignals > 10 && (
                    <div className="px-4 py-2.5 border-t border-gray-800">
                      <Link
                        to={`/signals?symbol=${upperSymbol}`}
                        className="text-xs text-axon-400 hover:text-axon-300 transition-colors"
                      >
                        Bekijk alle signalen →
                      </Link>
                    </div>
                  )}
                </>
              )}
            </div>
          </section>
        </div>
      )}

      {activeTab === 'Nieuws' && (
        <section>
          <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">
            Nieuws voor {upperSymbol}
            {news.length > 0 && (
              <span className="ml-2 px-2 py-0.5 rounded-full bg-gray-800 text-gray-400 text-xs font-normal">{news.length}</span>
            )}
          </h2>
          <NewsTab news={news} />
        </section>
      )}

      {activeTab === 'Fundamentals' && (
        <div className="space-y-6">
          {fundLoading ? (
            <Skeleton />
          ) : fundError || !fund ? (
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 text-center">
              <p className="text-gray-400">Geen fundamentals beschikbaar voor {upperSymbol}.</p>
              <p className="text-xs text-gray-600 mt-1">Controleer of een fundamentals provider actief is.</p>
            </div>
          ) : (
            <>
              <section>
                <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Waardering</h2>
                <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
                  <MetricCard label="P/E Ratio"    value={fmt(fund.peRatio)}    color={peColor(fund.peRatio)}    tooltip={TOOLTIPS.peRatio} />
                  <MetricCard label="Forward P/E"  value={fmt(fund.forwardPe)}  color={peColor(fund.forwardPe)}  tooltip={TOOLTIPS.forwardPe} />
                  <MetricCard label="P/B Ratio"    value={fmt(fund.pbRatio)}    color={fund.pbRatio != null && fund.pbRatio < 3 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.pbRatio} />
                  <MetricCard label="P/S Ratio"    value={fmt(fund.psRatio)}    color={fund.psRatio != null && fund.psRatio < 2 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.psRatio} />
                  <MetricCard label="EV/EBITDA"    value={fmt(fund.evToEbitda)} color={fund.evToEbitda != null && fund.evToEbitda < 15 ? 'text-green-400' : 'text-yellow-400'} tooltip={TOOLTIPS.evToEbitda} />
                </div>
              </section>

              <section>
                <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Winstgevendheid & Groei</h2>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  <MetricCard label="Nettomarge"         value={fmtPct(fund.profitMargin)}      color={pctColor(fund.profitMargin)}      tooltip={TOOLTIPS.profitMargin} />
                  <MetricCard label="Operationele marge" value={fmtPct(fund.operatingMargin)}   color={pctColor(fund.operatingMargin)}   tooltip={TOOLTIPS.operatingMargin} />
                  <MetricCard label="ROE"                value={fmtPct(fund.returnOnEquity)}    color={pctColor(fund.returnOnEquity)}    tooltip={TOOLTIPS.returnOnEquity} />
                  <MetricCard label="ROA"                value={fmtPct(fund.returnOnAssets)}    color={pctColor(fund.returnOnAssets)}    tooltip={TOOLTIPS.returnOnAssets} />
                  <MetricCard label="Omzetgroei (YoY)"   value={fmtPct(fund.revenueGrowthYoy)}  color={pctColor(fund.revenueGrowthYoy)}  tooltip={TOOLTIPS.revenueGrowthYoy} />
                  <MetricCard label="Winstgroei (YoY)"   value={fmtPct(fund.earningsGrowthYoy)} color={pctColor(fund.earningsGrowthYoy)} tooltip={TOOLTIPS.earningsGrowthYoy} />
                </div>
              </section>

              <section>
                <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Balans</h2>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  <MetricCard label="Schuld/Eigen vermogen" value={fmt(fund.debtToEquity)} color={deColor(fund.debtToEquity)}     tooltip={TOOLTIPS.debtToEquity} />
                  <MetricCard label="Current Ratio"         value={fmt(fund.currentRatio)} color={ratioColor(fund.currentRatio)} tooltip={TOOLTIPS.currentRatio} />
                  <MetricCard label="Quick Ratio"           value={fmt(fund.quickRatio)}   color={ratioColor(fund.quickRatio)}   tooltip={TOOLTIPS.quickRatio} />
                </div>
              </section>

              {(fund.dividendYield != null || fund.payoutRatio != null) && (
                <section>
                  <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Dividend</h2>
                  <div className="grid grid-cols-2 gap-3 max-w-xs">
                    <MetricCard label="Dividendrendement" value={fmtPct(fund.dividendYield)} color="text-white"                    tooltip={TOOLTIPS.dividendYield} />
                    <MetricCard label="Uitkeringsratio"   value={fmtPct(fund.payoutRatio)}   color={payoutColor(fund.payoutRatio)} tooltip={TOOLTIPS.payoutRatio} />
                  </div>
                </section>
              )}

              <section>
                <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Omvang</h2>
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  <MetricCard label="Marktkapitalisatie" value={fmtLarge(fund.marketCap) !== '—' ? '$' + fmtLarge(fund.marketCap) : '—'} color="text-white"                tooltip={TOOLTIPS.marketCap} />
                  <MetricCard label="Omzet (TTM)"        value={fund.revenue   != null ? '$' + fmtLarge(fund.revenue)   : '—'} color="text-white"                tooltip={TOOLTIPS.revenue} />
                  <MetricCard label="Nettoresultaat"     value={fund.netIncome != null ? '$' + fmtLarge(fund.netIncome) : '—'} color={pctColor(fund.netIncome)} tooltip={TOOLTIPS.netIncome} />
                </div>
              </section>

              <section>
                <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3 flex items-center">Analisten consensus<InfoTooltip text={TOOLTIPS.analystConsensus} /></h2>
                <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
                  <AnalystBar data={fund} />
                </div>
              </section>
            </>
          )}

          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Insider transacties</h2>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              {insidersLoading ? (
                <div className="animate-pulse space-y-2">
                  {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-8 bg-gray-800 rounded" />)}
                </div>
              ) : (
                <InsiderTable transactions={insiders} />
              )}
            </div>
          </section>
        </div>
      )}

      {activeTab === 'Profiel' && (
        <ProfileTab watchlistItem={watchlistItem} fund={fund} />
      )}
    </div>
  );
}
