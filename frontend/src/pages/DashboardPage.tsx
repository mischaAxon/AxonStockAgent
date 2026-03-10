import { useDashboard } from '../hooks/useApi';
import { TrendingUp, TrendingDown, Zap, Eye, Briefcase } from 'lucide-react';

export default function DashboardPage() {
  const { data, isLoading, error } = useDashboard();

  if (isLoading) return <div className="text-gray-400">Laden...</div>;
  if (error) return <div className="text-red-400">Fout bij laden dashboard</div>;

  const d = data?.data;
  if (!d) return null;

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Dashboard</h2>

      {/* Stats cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard icon={<Eye size={20} />} label="Watchlist" value={d.watchlistCount} color="blue" />
        <StatCard icon={<Briefcase size={20} />} label="Portfolio" value={d.portfolioPositions} color="purple" />
        <StatCard icon={<TrendingUp size={20} />} label="BUY deze week" value={d.signals.weekBuys} color="green" />
        <StatCard icon={<TrendingDown size={20} />} label="SELL deze week" value={d.signals.weekSells} color="red" />
      </div>

      {/* Recent signals */}
      <div className="bg-gray-900 rounded-xl border border-gray-800 p-6 mb-6">
        <h3 className="text-lg font-semibold text-white mb-4">Recente Signalen</h3>
        {d.recentSignals.length === 0 ? (
          <p className="text-gray-500">Nog geen signalen. De scanner draait op de achtergrond.</p>
        ) : (
          <div className="space-y-3">
            {d.recentSignals.map((signal: any) => (
              <div key={signal.symbol + signal.createdAt} className="flex items-center justify-between bg-gray-800/50 rounded-lg px-4 py-3">
                <div className="flex items-center gap-3">
                  <VerdictBadge verdict={signal.finalVerdict} />
                  <div>
                    <span className="font-mono font-semibold text-white">{signal.symbol}</span>
                    {signal.claudeReasoning && (
                      <p className="text-xs text-gray-400 mt-0.5 max-w-md truncate">{signal.claudeReasoning}</p>
                    )}
                  </div>
                </div>
                <div className="text-right">
                  <div className="text-sm font-medium text-white">&euro;{signal.priceAtSignal?.toFixed(2)}</div>
                  <div className="text-xs text-gray-500">{new Date(signal.createdAt).toLocaleDateString('nl-NL')}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function StatCard({ icon, label, value, color }: { icon: React.ReactNode; label: string; value: number; color: string }) {
  const colors: Record<string, string> = {
    blue: 'bg-blue-500/10 text-blue-400 border-blue-500/20',
    green: 'bg-green-500/10 text-green-400 border-green-500/20',
    red: 'bg-red-500/10 text-red-400 border-red-500/20',
    purple: 'bg-purple-500/10 text-purple-400 border-purple-500/20',
  };

  return (
    <div className={`rounded-xl border p-5 ${colors[color]}`}>
      <div className="flex items-center gap-2 mb-2 opacity-80">{icon}<span className="text-sm">{label}</span></div>
      <div className="text-3xl font-bold">{value}</div>
    </div>
  );
}

function VerdictBadge({ verdict }: { verdict: string }) {
  const styles: Record<string, string> = {
    BUY: 'bg-green-500/20 text-green-400',
    SELL: 'bg-red-500/20 text-red-400',
    SQUEEZE: 'bg-amber-500/20 text-amber-400',
  };
  return (
    <span className={`px-2 py-1 rounded text-xs font-bold ${styles[verdict] || 'bg-gray-700 text-gray-300'}`}>
      {verdict}
    </span>
  );
}
