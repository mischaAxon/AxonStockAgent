import { useState } from 'react';
import { usePortfolio, useUpsertPortfolio } from '../hooks/useApi';
import { Plus } from 'lucide-react';

export default function PortfolioPage() {
  const { data, isLoading } = usePortfolio();
  const upsertMutation = useUpsertPortfolio();
  const [form, setForm] = useState({ symbol: '', shares: '', price: '' });

  const handleAdd = () => {
    if (!form.symbol || !form.shares) return;
    upsertMutation.mutate({
      symbol: form.symbol.toUpperCase(),
      shares: parseInt(form.shares),
      avgBuyPrice: form.price ? parseFloat(form.price) : undefined,
    });
    setForm({ symbol: '', shares: '', price: '' });
  };

  const totalValue = (data?.data || []).reduce(
    (sum, p) => sum + p.shares * (p.avgBuyPrice || 0), 0
  );

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Portfolio</h2>

      {/* Add position */}
      <div className="flex gap-2 mb-6 flex-wrap">
        <input type="text" value={form.symbol} onChange={e => setForm({...form, symbol: e.target.value})}
          placeholder="Symbol" className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-500 w-40" />
        <input type="number" value={form.shares} onChange={e => setForm({...form, shares: e.target.value})}
          placeholder="Aantal" className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-500 w-28" />
        <input type="number" step="0.01" value={form.price} onChange={e => setForm({...form, price: e.target.value})}
          placeholder="Gem. prijs" className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-500 w-36" />
        <button onClick={handleAdd} className="bg-axon-600 hover:bg-axon-700 text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-medium">
          <Plus size={16} /> Toevoegen
        </button>
      </div>

      {/* Portfolio summary */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-6">
        <p className="text-gray-400 text-sm">Geschatte portefeuillewaarde</p>
        <p className="text-3xl font-bold text-white">&euro;{totalValue.toLocaleString('nl-NL', { minimumFractionDigits: 2 })}</p>
      </div>

      {isLoading ? <p className="text-gray-400">Laden...</p> : (
        <div className="bg-gray-900 rounded-xl border border-gray-800 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-3 text-left text-gray-400">Symbol</th>
                <th className="px-4 py-3 text-right text-gray-400">Aandelen</th>
                <th className="px-4 py-3 text-right text-gray-400">Gem. Prijs</th>
                <th className="px-4 py-3 text-right text-gray-400">Waarde</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {(data?.data || []).map(p => (
                <tr key={p.id} className="hover:bg-gray-800/30">
                  <td className="px-4 py-3 font-mono font-semibold text-white">{p.symbol}</td>
                  <td className="px-4 py-3 text-right text-gray-300">{p.shares}</td>
                  <td className="px-4 py-3 text-right text-gray-300">{p.avgBuyPrice ? `\u20AC${p.avgBuyPrice.toFixed(2)}` : '-'}</td>
                  <td className="px-4 py-3 text-right font-medium text-white">{p.avgBuyPrice ? `\u20AC${(p.shares * p.avgBuyPrice).toFixed(2)}` : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
