import { useState } from 'react';
import { useWatchlist, useAddToWatchlist, useRemoveFromWatchlist } from '../hooks/useApi';
import { Plus, X } from 'lucide-react';

export default function WatchlistPage() {
  const { data, isLoading } = useWatchlist();
  const addMutation = useAddToWatchlist();
  const removeMutation = useRemoveFromWatchlist();
  const [newSymbol, setNewSymbol] = useState('');

  const handleAdd = () => {
    if (!newSymbol.trim()) return;
    addMutation.mutate({ symbol: newSymbol.trim().toUpperCase() });
    setNewSymbol('');
  };

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Watchlist</h2>

      {/* Add symbol */}
      <div className="flex gap-2 mb-6">
        <input
          type="text"
          value={newSymbol}
          onChange={(e) => setNewSymbol(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleAdd()}
          placeholder="Symbol toevoegen (bijv. ASML.AS)"
          className="bg-gray-800 border border-gray-700 rounded-lg px-4 py-2 text-white placeholder-gray-500 focus:outline-none focus:border-axon-500 w-72"
        />
        <button onClick={handleAdd} className="bg-axon-600 hover:bg-axon-700 text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-medium transition-colors">
          <Plus size={16} /> Toevoegen
        </button>
      </div>

      {isLoading ? (
        <p className="text-gray-400">Laden...</p>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {(data?.data || []).map((item) => (
            <div key={item.id} className="bg-gray-900 border border-gray-800 rounded-xl p-4 flex items-center justify-between">
              <div>
                <span className="font-mono font-bold text-white text-lg">{item.symbol}</span>
                {item.name && <p className="text-sm text-gray-400">{item.name}</p>}
                {item.exchange && <p className="text-xs text-gray-500">{item.exchange}</p>}
              </div>
              <button
                onClick={() => removeMutation.mutate(item.symbol)}
                className="text-gray-500 hover:text-red-400 transition-colors p-1"
              >
                <X size={18} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
