import { useState } from 'react';
import { useSignals } from '../hooks/useApi';

export default function SignalsPage() {
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<string>('');
  const { data, isLoading } = useSignals(page, 20, undefined, filter || undefined);

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-white">Signalen</h2>
        <div className="flex gap-2">
          {['', 'BUY', 'SELL', 'SQUEEZE'].map((v) => (
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
        </div>
      </div>

      {isLoading ? (
        <p className="text-gray-400">Laden...</p>
      ) : (
        <div className="bg-gray-900 rounded-xl border border-gray-800 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Datum</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Symbol</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">Verdict</th>
                <th className="px-4 py-3 text-right text-gray-400 font-medium">Score</th>
                <th className="px-4 py-3 text-right text-gray-400 font-medium">Prijs</th>
                <th className="px-4 py-3 text-left text-gray-400 font-medium">AI Reasoning</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {(data?.data || []).map((s) => (
                <tr key={s.id} className="hover:bg-gray-800/30">
                  <td className="px-4 py-3 text-gray-400">{new Date(s.createdAt).toLocaleString('nl-NL', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}</td>
                  <td className="px-4 py-3 font-mono font-semibold text-white">{s.symbol}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-xs font-bold ${
                      s.finalVerdict === 'BUY' ? 'bg-green-500/20 text-green-400' :
                      s.finalVerdict === 'SELL' ? 'bg-red-500/20 text-red-400' :
                      'bg-amber-500/20 text-amber-400'
                    }`}>{s.finalVerdict}</span>
                  </td>
                  <td className="px-4 py-3 text-right font-mono">{(s.finalScore * 100).toFixed(1)}%</td>
                  <td className="px-4 py-3 text-right font-mono text-white">&euro;{s.priceAtSignal.toFixed(2)}</td>
                  <td className="px-4 py-3 text-gray-400 max-w-xs truncate">{s.claudeReasoning || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {data?.meta && (
            <div className="flex items-center justify-between px-4 py-3 bg-gray-800/30">
              <span className="text-sm text-gray-500">{data.meta.total} signalen totaal</span>
              <div className="flex gap-2">
                <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}
                  className="px-3 py-1 rounded bg-gray-800 text-gray-400 disabled:opacity-30">Vorige</button>
                <button onClick={() => setPage(p => p + 1)} disabled={data.data.length < 20}
                  className="px-3 py-1 rounded bg-gray-800 text-gray-400 disabled:opacity-30">Volgende</button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
