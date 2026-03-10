import { useEffect, useRef } from 'react';
import { useLatestNews } from '../hooks/useApi';
import { ExternalLink } from 'lucide-react';

function formatRelativeTime(date: string | Date): string {
  const now = Date.now();
  const then = new Date(date).getTime();
  const diff = Math.floor((now - then) / 1000);
  if (diff < 60) return `${diff}s geleden`;
  if (diff < 3600) return `${Math.floor(diff / 60)} min geleden`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} uur geleden`;
  return `${Math.floor(diff / 86400)}d geleden`;
}

function SentimentDot({ score }: { score: number }) {
  if (score > 0.1) return <span className="w-2 h-2 rounded-full bg-green-400 inline-block flex-shrink-0" />;
  if (score < -0.1) return <span className="w-2 h-2 rounded-full bg-red-400 inline-block flex-shrink-0" />;
  return <span className="w-2 h-2 rounded-full bg-gray-500 inline-block flex-shrink-0" />;
}

export function NewsTicker() {
  const { data: articles = [] } = useLatestNews(10);
  const trackRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const track = trackRef.current;
    if (!track || articles.length === 0) return;

    let x = 0;
    let animId: number;
    const speed = 0.5;

    const animate = () => {
      x -= speed;
      const totalWidth = track.scrollWidth / 2;
      if (Math.abs(x) >= totalWidth) x = 0;
      track.style.transform = `translateX(${x}px)`;
      animId = requestAnimationFrame(animate);
    };

    animId = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(animId);
  }, [articles]);

  if (articles.length === 0) return null;

  const items = [...articles, ...articles];

  return (
    <div className="bg-gray-900 border-b border-gray-800 h-9 flex items-center overflow-hidden relative">
      <div className="flex-shrink-0 px-3 text-xs font-semibold text-axon-400 uppercase tracking-wider border-r border-gray-700 h-full flex items-center">
        Nieuws
      </div>
      <div className="flex-1 overflow-hidden relative">
        <div ref={trackRef} className="flex items-center gap-8 whitespace-nowrap will-change-transform">
          {items.map((article, i) => (
            <a
              key={`${article.id}-${i}`}
              href={article.url ?? '#'}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-2 text-xs text-gray-300 hover:text-white transition-colors flex-shrink-0"
            >
              <SentimentDot score={article.sentimentScore} />
              {article.symbol && (
                <span className="bg-gray-700 text-gray-200 px-1.5 py-0.5 rounded text-[10px] font-mono font-semibold">
                  {article.symbol}
                </span>
              )}
              <span className="max-w-xs truncate">{article.headline}</span>
              <span className="text-gray-500">{formatRelativeTime(article.publishedAt)}</span>
              {article.url && <ExternalLink className="w-3 h-3 text-gray-600 flex-shrink-0" />}
            </a>
          ))}
        </div>
      </div>
    </div>
  );
}
