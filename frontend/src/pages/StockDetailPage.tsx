import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, RefreshCw, ExternalLink } from 'lucide-react';
import { useFundamentals, useInsiderTransactions, useWatchlist } from '../hooks/useApi';
import type { CompanyFundamentals, InsiderTransaction } from '../types';

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

function MetricCard({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
      <p className="text-xs text-gray-500 mb-1">{label}</p>
      <p className={`text-xl font-bold ${color}`}>{value}</p>
    </div>
  );
}

function Skeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-8 bg-gray-800 rounded w-48" />
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-20 bg-gray-800 rounded-xl" />
        ))}
      </div>
    </div>
  );
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
          <p className="text-xs text-gray-500">Koersdoel analisten</p>
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

// ─── main page ───────────────────────────────────────────────────────────────

export default function StockDetailPage() {
  const { symbol = '' } = useParams<{ symbol: string }>();
  const upperSymbol = symbol.toUpperCase();

  const { data: watchlistData }   = useWatchlist();
  const { data: fundData, isLoading: fundLoading, error: fundError, refetch } = useFundamentals(upperSymbol);
  const { data: insidersData, isLoading: insidersLoading } = useInsiderTransactions(upperSymbol);

  const watchlistItem = watchlistData?.data?.find(w => w.symbol === upperSymbol);
  const fund: CompanyFundamentals | undefined = fundData?.data;
  const insiders: InsiderTransaction[] = insidersData?.data ?? [];

  return (
    <div className="space-y-6">
      {/* Back link */}
      <Link to="/watchlist" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white transition-colors">
        <ArrowLeft size={14} /> Terug naar Watchlist
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
          {watchlistItem?.webUrl && (
            <a href={watchlistItem.webUrl} target="_blank" rel="noopener noreferrer"
               className="p-2 rounded-lg bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-white transition-colors">
              <ExternalLink size={14} />
            </a>
          )}
          <button
            onClick={() => refetch()}
            className="p-2 rounded-lg bg-gray-800 hover:bg-gray-700 text-gray-400 hover:text-white transition-colors"
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

      {fundLoading ? (
        <Skeleton />
      ) : fundError || !fund ? (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 text-center">
          <p className="text-gray-400">Geen fundamentals beschikbaar voor {upperSymbol}.</p>
          <p className="text-xs text-gray-600 mt-1">Controleer of een fundamentals provider actief is.</p>
        </div>
      ) : (
        <div className="space-y-6">
          {/* Valuation */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Waardering</h2>
            <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
              <MetricCard label="P/E Ratio"    value={fmt(fund.peRatio)}    color={peColor(fund.peRatio)} />
              <MetricCard label="Forward P/E"  value={fmt(fund.forwardPe)}  color={peColor(fund.forwardPe)} />
              <MetricCard label="P/B Ratio"    value={fmt(fund.pbRatio)}    color={fund.pbRatio != null && fund.pbRatio < 3 ? 'text-green-400' : 'text-yellow-400'} />
              <MetricCard label="P/S Ratio"    value={fmt(fund.psRatio)}    color={fund.psRatio != null && fund.psRatio < 2 ? 'text-green-400' : 'text-yellow-400'} />
              <MetricCard label="EV/EBITDA"    value={fmt(fund.evToEbitda)} color={fund.evToEbitda != null && fund.evToEbitda < 15 ? 'text-green-400' : 'text-yellow-400'} />
            </div>
          </section>

          {/* Profitability & Growth */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Winstgevendheid & Groei</h2>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              <MetricCard label="Nettomarge"      value={fmtPct(fund.profitMargin)}      color={pctColor(fund.profitMargin)} />
              <MetricCard label="Operationele marge" value={fmtPct(fund.operatingMargin)} color={pctColor(fund.operatingMargin)} />
              <MetricCard label="ROE"             value={fmtPct(fund.returnOnEquity)}    color={pctColor(fund.returnOnEquity)} />
              <MetricCard label="ROA"             value={fmtPct(fund.returnOnAssets)}    color={pctColor(fund.returnOnAssets)} />
              <MetricCard label="Omzetgroei (YoY)" value={fmtPct(fund.revenueGrowthYoy)} color={pctColor(fund.revenueGrowthYoy)} />
              <MetricCard label="Winstgroei (YoY)" value={fmtPct(fund.earningsGrowthYoy)} color={pctColor(fund.earningsGrowthYoy)} />
            </div>
          </section>

          {/* Balance sheet */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Balans</h2>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              <MetricCard label="Schuld/Eigen vermogen" value={fmt(fund.debtToEquity)} color={deColor(fund.debtToEquity)} />
              <MetricCard label="Current Ratio"  value={fmt(fund.currentRatio)} color={ratioColor(fund.currentRatio)} />
              <MetricCard label="Quick Ratio"    value={fmt(fund.quickRatio)}   color={ratioColor(fund.quickRatio)} />
            </div>
          </section>

          {/* Dividends */}
          {(fund.dividendYield != null || fund.payoutRatio != null) && (
            <section>
              <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Dividend</h2>
              <div className="grid grid-cols-2 gap-3 max-w-xs">
                <MetricCard label="Dividendrendement" value={fmtPct(fund.dividendYield)} color="text-white" />
                <MetricCard label="Uitkeringsratio"   value={fmtPct(fund.payoutRatio)}   color={payoutColor(fund.payoutRatio)} />
              </div>
            </section>
          )}

          {/* Size */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Omvang</h2>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              <MetricCard label="Marktkapitalisatie" value={fmtLarge(fund.marketCap) !== '—' ? '$' + fmtLarge(fund.marketCap) : '—'} color="text-white" />
              <MetricCard label="Omzet (TTM)"        value={fund.revenue  != null ? '$' + fmtLarge(fund.revenue)  : '—'} color="text-white" />
              <MetricCard label="Nettoresultaat"     value={fund.netIncome != null ? '$' + fmtLarge(fund.netIncome) : '—'} color={pctColor(fund.netIncome)} />
            </div>
          </section>

          {/* Analyst consensus */}
          <section>
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Analisten consensus</h2>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
              <AnalystBar data={fund} />
            </div>
          </section>
        </div>
      )}

      {/* Insider Trading */}
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
  );
}
