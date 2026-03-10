import { useState } from 'react';
import { useSectorSentiment, useTrending, useLatestNews } from '../hooks/useApi';
import { Newspaper, TrendingUp, ExternalLink } from 'lucide-react';

function formatRelativeTime(date: string | Date): string {
  const now = Date.now();
  const then = new Date(date).getTime();
  const diff = Math.floor((now - then) / 1000);
  if (diff < 60) return `${diff}s geleden`;
  if (diff < 3600) return `${Math.floor(diff / 60)} min geleden`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} uur geleden`;
  return `${Math.floor(diff / 86400)}d geleden`;
}

function sentimentColor(score: number): string {
  if (score > 0.3) return 'bg-green-900/60 border-green-700 text-green-300';
  if (score > 0.1) return 'bg-green-900/30 border-green-800 text-green-400';
  if (score < -0.3) return 'bg-red-900/60 border-red-700 text-red-300';
  if (score < -0.1) return 'bg-red-900/30 border-red-800 text-red-400';
  return 'bg-gray-800 border-gray-700 text-gray-400';
}

function sentimentLabel(score: number): string {
  if (score > 0.3) return 'Bullish';
  if (score > 0.1) return 'Licht bullish';
  if (score < -0.3) return 'Bearish';
  if (score < -0.1) return 'Licht bearish';
  return 'Neutraal';
}

function SentimentDot({ score }: { score: number }) {
  if (score > 0.1) return <span className="w-2 h-2 rounded-full bg-green-400 inline-block flex-shrink-0 mt-0.5" />;
  if (score < -0.1) return <span className="w-2 h-2 rounded-full bg-red-400 inline-block flex-shrink-0 mt-0.5" />;
  return <span className="w-2 h-2 rounded-full bg-gray-500 inline-block flex-shrink-0 mt-0.5" />;
}

export default function NewsPage() {
  const { data: sentiment = [] } = useSectorSentiment();
  const { data: trending = [] } = useTrending();
  const { data: articles = [] } = useLatestNews(50);
  const [sectorFilter, setSectorFilter] = useState<string>('');

  const sectors = Array.from(new Set(articles.map(a => a.sector).filter(Boolean) as string[]));
  const filtered = sectorFilter ? articles.filter(a => a.sector === sectorFilter) : articles;

  return (
    <div className="p-6 space-y-8">
      <div className="flex items-center gap-3">
        <Newspaper className="w-6 h-6 text-axon-400" />
        <h1 className="text-2xl font-bold text-white">Nieuws</h1>
      </div>

      {/* Sector Sentiment Heatmap */}
      <section>
        <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Sector Sentiment (24u)</h2>
        {sentiment.length === 0 ? (
          <p className="text-gray-500 text-sm">Nog geen sentiment data beschikbaar.</p>
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
            {sentiment.map(s => (
              <div key={s.sector} className={`border rounded-lg p-3 ${sentimentColor(s.avgSentiment)}`}>
                <div className="font-semibold text-sm truncate">{s.sector}</div>
                <div className="text-xs mt-1 opacity-80">{sentimentLabel(s.avgSentiment)}</div>
                <div className="text-xs mt-1 opacity-60">{s.articleCount} artikelen</div>
                <div className="text-xs font-mono mt-1">{(s.avgSentiment * 100).toFixed(0)}%</div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Trending Symbols */}
      {trending.length > 0 && (
        <section>
          <div className="flex items-center gap-2 mb-3">
            <TrendingUp className="w-4 h-4 text-axon-400" />
            <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Trending Symbolen (24u)</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            {trending.map(t => (
              <div key={t.symbol} className="flex items-center gap-2 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2">
                <span className="font-mono font-semibold text-white text-sm">{t.symbol}</span>
                <span className="text-xs text-gray-400">{t.articleCount} artikelen</span>
                <span className={`text-xs ${t.avgSentiment > 0.1 ? 'text-green-400' : t.avgSentiment < -0.1 ? 'text-red-400' : 'text-gray-500'}`}>
                  {(t.avgSentiment * 100).toFixed(0)}%
                </span>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* News Feed */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Nieuwsfeed</h2>
          {sectors.length > 0 && (
            <select
              value={sectorFilter}
              onChange={e => setSectorFilter(e.target.value)}
              className="bg-gray-800 border border-gray-700 text-gray-300 text-xs rounded-lg px-2 py-1 focus:outline-none focus:border-axon-500"
            >
              <option value="">Alle sectoren</option>
              {sectors.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          )}
        </div>

        {filtered.length === 0 ? (
          <p className="text-gray-500 text-sm">Geen artikelen gevonden.</p>
        ) : (
          <div className="space-y-2">
            {filtered.map(article => (
              <a
                key={article.id}
                href={article.url ?? '#'}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-start gap-3 bg-gray-900 border border-gray-800 rounded-lg p-3 hover:border-gray-600 transition-colors group"
              >
                <SentimentDot score={article.sentimentScore} />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap mb-1">
                    {article.symbol && (
                      <span className="bg-gray-700 text-gray-200 px-1.5 py-0.5 rounded text-[10px] font-mono font-semibold">
                        {article.symbol}
                      </span>
                    )}
                    {article.sector && (
                      <span className="bg-gray-800 text-gray-400 px-1.5 py-0.5 rounded text-[10px]">
                        {article.sector}
                      </span>
                    )}
                    <span className="text-[10px] text-gray-500">{article.source}</span>
                    <span className="text-[10px] text-gray-600">{formatRelativeTime(article.publishedAt)}</span>
                  </div>
                  <p className="text-sm text-gray-200 group-hover:text-white transition-colors leading-snug">
                    {article.headline}
                  </p>
                  {article.summary && (
                    <p className="text-xs text-gray-500 mt-1 line-clamp-2">{article.summary}</p>
                  )}
                </div>
                {article.url && <ExternalLink className="w-3.5 h-3.5 text-gray-600 flex-shrink-0 mt-0.5 group-hover:text-gray-400 transition-colors" />}
              </a>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
