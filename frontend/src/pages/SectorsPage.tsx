import {
  Cpu, Heart, TrendingUp, ShoppingCart, ShoppingBag,
  Zap, Factory, Layers, Building2, Wifi, MessageCircle,
  BarChart2, RefreshCw,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useSectors, useWatchlist, useEnrichWatchlist } from '../hooks/useApi';
import { useAuth } from '../contexts/AuthContext';
import type { WatchlistItem } from '../types';

type SectorSummary = { sector: string; count: number };

interface SectorConfig {
  Icon:  LucideIcon;
  color: string;
  bg:    string;
}

const SECTOR_CONFIG: Record<string, SectorConfig> = {
  'Technology':             { Icon: Cpu,            color: 'text-blue-400',    bg: 'bg-blue-500/10'    },
  'Healthcare':             { Icon: Heart,          color: 'text-green-400',   bg: 'bg-green-500/10'   },
  'Financials':             { Icon: TrendingUp,     color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
  'Finance':                { Icon: TrendingUp,     color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
  'Consumer Staples':       { Icon: ShoppingCart,   color: 'text-orange-400',  bg: 'bg-orange-500/10'  },
  'Consumer Discretionary': { Icon: ShoppingBag,    color: 'text-amber-400',   bg: 'bg-amber-500/10'   },
  'Energy':                 { Icon: Zap,            color: 'text-yellow-400',  bg: 'bg-yellow-500/10'  },
  'Industrials':            { Icon: Factory,        color: 'text-slate-400',   bg: 'bg-slate-500/10'   },
  'Materials':              { Icon: Layers,         color: 'text-stone-400',   bg: 'bg-stone-500/10'   },
  'Real Estate':            { Icon: Building2,      color: 'text-rose-400',    bg: 'bg-rose-500/10'    },
  'Utilities':              { Icon: Wifi,           color: 'text-cyan-400',    bg: 'bg-cyan-500/10'    },
  'Communication Services': { Icon: MessageCircle,  color: 'text-purple-400',  bg: 'bg-purple-500/10'  },
};

const DEFAULT_CONFIG: SectorConfig = {
  Icon: BarChart2, color: 'text-gray-400', bg: 'bg-gray-500/10',
};

export default function SectorsPage() {
  const { data: sectorsData, isLoading } = useSectors();
  const { data: watchlistData }          = useWatchlist();
  const enrichMutation                   = useEnrichWatchlist();
  const { isAdmin }                      = useAuth();

  const sectors: SectorSummary[] =
    (sectorsData as { data?: SectorSummary[] } | undefined)?.data ?? [];
  const watchlistItems: WatchlistItem[] = watchlistData?.data ?? [];

  return (
    <div>
      {/* Header */}
      <div className="flex items-start justify-between mb-8 gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Sectoren</h1>
          <p className="text-gray-400 text-sm mt-1">Verdeling van de watchlist per sector</p>
        </div>
        {isAdmin && (
          <button
            onClick={() => enrichMutation.mutate()}
            disabled={enrichMutation.isPending}
            className="flex items-center gap-2 px-4 py-2 bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white text-sm rounded-lg transition-colors whitespace-nowrap"
          >
            <RefreshCw size={14} className={enrichMutation.isPending ? 'animate-spin' : ''} />
            {enrichMutation.isPending ? 'Bezig…' : 'Verrijk watchlist'}
          </button>
        )}
      </div>

      {/* Resultaat verrijking */}
      {enrichMutation.isSuccess && (
        <div className="mb-6 bg-green-500/10 border border-green-500/20 rounded-lg px-4 py-3 text-sm text-green-400">
          ✓ {(enrichMutation.data as { data?: { enriched: number } })?.data?.enriched ?? 0} symbolen verrijkt
        </div>
      )}

      {isLoading && <p className="text-gray-400 text-sm">Laden…</p>}

      {!isLoading && sectors.length === 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-10 text-center">
          <BarChart2 size={32} className="text-gray-700 mx-auto mb-3" />
          <p className="text-gray-400 text-sm">Nog geen sector data beschikbaar.</p>
          {isAdmin && (
            <p className="text-gray-500 text-xs mt-2">
              Schakel een fundamentals provider in en klik „Verrijk watchlist”.
            </p>
          )}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
        {sectors.map(({ sector, count }) => {
          const cfg     = SECTOR_CONFIG[sector] ?? DEFAULT_CONFIG;
          const { Icon } = cfg;
          const symbols = watchlistItems.filter(w => w.sector === sector);

          return (
            <div key={sector} className="bg-gray-900 border border-gray-800 rounded-xl p-5">
              {/* Sector header */}
              <div className="flex items-center gap-3 mb-4">
                <div className={`w-10 h-10 rounded-xl ${cfg.bg} flex items-center justify-center flex-shrink-0`}>
                  <Icon size={18} className={cfg.color} />
                </div>
                <div>
                  <h3 className="text-sm font-semibold text-white">{sector}</h3>
                  <p className="text-xs text-gray-500">
                    {count} aandeel{count !== 1 ? 'en' : ''}
                  </p>
                </div>
              </div>

              {/* Symbolen */}
              <div className="flex flex-wrap gap-1.5">
                {symbols.map(w => (
                  <span
                    key={w.symbol}
                    className="px-2 py-0.5 rounded text-xs font-mono bg-gray-800 text-gray-300"
                    title={w.name ?? undefined}
                  >
                    {w.symbol}
                  </span>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
