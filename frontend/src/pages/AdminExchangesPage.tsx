import { useState } from 'react';
import { Plus, Trash2, Download, Globe } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';

interface TrackedExchange {
  id: number;
  exchangeCode: string;
  displayName: string;
  country: string;
  isEnabled: boolean;
  symbolCount: number;
  lastImportAt: string | null;
}

interface MarketIndexAdmin {
  id: number;
  indexSymbol: string;
  displayName: string;
  exchangeCode: string;
  country: string;
  symbolCount: number;
  isEnabled: boolean;
  lastImportAt: string | null;
}

// Veelgebruikte EODHD exchange codes
const EXCHANGE_PRESETS = [
  { code: 'AS', name: 'Euronext Amsterdam', country: 'NL' },
  { code: 'US', name: 'NYSE + NASDAQ (gecombineerd)', country: 'US' },
  { code: 'XETRA', name: 'XETRA (Frankfurt)', country: 'DE' },
  { code: 'LSE', name: 'London Stock Exchange', country: 'GB' },
  { code: 'PA', name: 'Euronext Paris', country: 'FR' },
  { code: 'MI', name: 'Borsa Italiana (Milan)', country: 'IT' },
  { code: 'SW', name: 'SIX Swiss Exchange', country: 'CH' },
  { code: 'TO', name: 'Toronto Stock Exchange', country: 'CA' },
  { code: 'HK', name: 'Hong Kong Stock Exchange', country: 'HK' },
  { code: 'TSE', name: 'Tokyo Stock Exchange', country: 'JP' },
  { code: 'BR', name: 'Euronext Brussels', country: 'BE' },
  { code: 'SN', name: 'Bolsa de Madrid', country: 'ES' },
  { code: 'ST', name: 'Nasdaq Stockholm', country: 'SE' },
  { code: 'CO', name: 'Nasdaq Copenhagen', country: 'DK' },
  { code: 'HE', name: 'Nasdaq Helsinki', country: 'FI' },
  { code: 'OL', name: 'Oslo Børs', country: 'NO' },
];

const INDEX_PRESETS = [
  { symbol: 'AEX.INDX',   name: 'AEX 25',        exchange: 'AS',    country: 'NL' },
  { symbol: 'AMX.INDX',   name: 'AMX Midcap',    exchange: 'AS',    country: 'NL' },
  { symbol: 'ASCX.INDX',  name: 'AScX Smallcap', exchange: 'AS',    country: 'NL' },
  { symbol: 'GSPC.INDX',  name: 'S&P 500',       exchange: 'US',    country: 'US' },
  { symbol: 'NDX.INDX',   name: 'NASDAQ-100',    exchange: 'US',    country: 'US' },
  { symbol: 'DJI.INDX',   name: 'Dow Jones 30',  exchange: 'US',    country: 'US' },
  { symbol: 'GDAXI.INDX', name: 'DAX 40',        exchange: 'XETRA', country: 'DE' },
  { symbol: 'FCHI.INDX',  name: 'CAC 40',        exchange: 'PA',    country: 'FR' },
  { symbol: 'FTSE.INDX',  name: 'FTSE 100',      exchange: 'LSE',   country: 'GB' },
];

export default function AdminExchangesPage() {
  const queryClient = useQueryClient();
  const [importing, setImporting] = useState<number | null>(null);
  const [importingIndex, setImportingIndex] = useState<number | null>(null);

  // ── Exchanges ──────────────────────────────────────────────────────────────

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'exchanges'],
    queryFn: () => api.get<{ data: TrackedExchange[] }>('/v1/admin/exchanges'),
  });

  const addMutation = useMutation({
    mutationFn: (exchange: { exchangeCode: string; displayName: string; country: string }) =>
      api.post('/v1/admin/exchanges', exchange),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.delete(`/v1/admin/exchanges/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isEnabled }: { id: number; isEnabled: boolean }) =>
      api.put(`/v1/admin/exchanges/${id}`, { isEnabled }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] }),
  });

  async function handleImport(id: number) {
    setImporting(id);
    try {
      await api.post(`/v1/admin/exchanges/${id}/import`, {});
      queryClient.invalidateQueries({ queryKey: ['admin', 'exchanges'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    } finally {
      setImporting(null);
    }
  }

  const exchanges: TrackedExchange[] = (data as any)?.data ?? [];
  const trackedCodes = new Set(exchanges.map(e => e.exchangeCode));
  const available = EXCHANGE_PRESETS.filter(p => !trackedCodes.has(p.code));

  // ── Indices ────────────────────────────────────────────────────────────────

  const { data: indicesData, isLoading: indicesLoading } = useQuery({
    queryKey: ['admin', 'indices'],
    queryFn: () => api.get<{ data: MarketIndexAdmin[] }>('/v1/admin/indices'),
  });

  const addIndexMutation = useMutation({
    mutationFn: (idx: { indexSymbol: string; displayName: string; exchangeCode: string; country: string }) =>
      api.post('/v1/admin/indices', idx),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] }),
  });

  const deleteIndexMutation = useMutation({
    mutationFn: (id: number) => api.delete(`/v1/admin/indices/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    },
  });

  async function handleImportIndex(id: number) {
    setImportingIndex(id);
    try {
      await api.post(`/v1/admin/indices/${id}/import`, {});
      queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
      queryClient.invalidateQueries({ queryKey: ['exchanges'] });
    } finally {
      setImportingIndex(null);
    }
  }

  const adminIndices: MarketIndexAdmin[] = (indicesData as any)?.data ?? [];
  const trackedIndexSymbols = new Set(adminIndices.map(i => i.indexSymbol));
  const availableIndices = INDEX_PRESETS.filter(p => !trackedIndexSymbols.has(p.symbol));

  return (
    <div>
      <h2 className="text-2xl font-bold text-white mb-6">Exchange Beheer</h2>
      <p className="text-sm text-gray-400 mb-6">
        Configureer welke beurzen en indexen automatisch worden geïmporteerd via EODHD.
        Importeer indexen om het Markets-scherm per index te groeperen.
      </p>

      {/* ── Indexen ────────────────────────────────────────────────────────── */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden mb-6">
        <div className="px-4 py-3 border-b border-gray-800">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Beursindexen</h3>
        </div>

        {indicesLoading ? (
          <div className="p-4 text-gray-500 text-sm">Laden...</div>
        ) : adminIndices.length === 0 ? (
          <div className="p-8 text-center">
            <Globe size={32} className="text-gray-700 mx-auto mb-3" />
            <p className="text-gray-400">Geen indexen geconfigureerd.</p>
            <p className="text-gray-600 text-sm">Voeg een index toe hieronder.</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-2 text-left text-gray-400">Naam</th>
                <th className="px-4 py-2 text-left text-gray-400">Symbool</th>
                <th className="px-4 py-2 text-left text-gray-400">Beurs</th>
                <th className="px-4 py-2 text-left text-gray-400">Land</th>
                <th className="px-4 py-2 text-right text-gray-400">Componenten</th>
                <th className="px-4 py-2 text-left text-gray-400">Laatste import</th>
                <th className="px-4 py-2 text-center text-gray-400">Acties</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {adminIndices.map(idx => (
                <tr key={idx.id} className="hover:bg-gray-800/30">
                  <td className="px-4 py-3 text-white font-medium">{idx.displayName}</td>
                  <td className="px-4 py-3 font-mono text-axon-400">{idx.indexSymbol}</td>
                  <td className="px-4 py-3 font-mono text-gray-400">{idx.exchangeCode}</td>
                  <td className="px-4 py-3 text-gray-400">{idx.country}</td>
                  <td className="px-4 py-3 text-right font-mono text-gray-300">{idx.symbolCount.toLocaleString()}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">
                    {idx.lastImportAt ? new Date(idx.lastImportAt).toLocaleString('nl-NL') : 'Nooit'}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        onClick={() => handleImportIndex(idx.id)}
                        disabled={importingIndex === idx.id}
                        className="p-1.5 rounded bg-blue-500/15 text-blue-400 hover:bg-blue-500/25 transition-colors disabled:opacity-50"
                        title="Importeer componenten"
                      >
                        <Download size={14} className={importingIndex === idx.id ? 'animate-bounce' : ''} />
                      </button>
                      <button
                        onClick={() => { if (window.confirm(`Index ${idx.displayName} verwijderen?`)) deleteIndexMutation.mutate(idx.id); }}
                        className="p-1.5 rounded text-gray-600 hover:text-red-400 transition-colors"
                        title="Verwijder index"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Add index */}
      {availableIndices.length > 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-4 mb-8">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Index Toevoegen</h3>
          <div className="flex flex-wrap gap-2">
            {availableIndices.map(preset => (
              <button
                key={preset.symbol}
                onClick={() => addIndexMutation.mutate({
                  indexSymbol: preset.symbol,
                  displayName: preset.name,
                  exchangeCode: preset.exchange,
                  country: preset.country,
                })}
                disabled={addIndexMutation.isPending}
                className="px-3 py-2 rounded-lg bg-gray-800 border border-gray-700 text-sm text-gray-300 hover:border-axon-500 hover:text-white transition-all flex items-center gap-2"
              >
                <Plus size={14} />
                <span className="font-mono text-axon-400">{preset.symbol}</span>
                <span className="text-gray-500">{preset.name}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* ── Actieve Beurzen ────────────────────────────────────────────────── */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden mb-6">
        <div className="px-4 py-3 border-b border-gray-800">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">Actieve Beurzen</h3>
        </div>

        {isLoading ? (
          <div className="p-4 text-gray-500 text-sm">Laden...</div>
        ) : exchanges.length === 0 ? (
          <div className="p-8 text-center">
            <Globe size={32} className="text-gray-700 mx-auto mb-3" />
            <p className="text-gray-400">Geen beurzen geconfigureerd.</p>
            <p className="text-gray-600 text-sm">Voeg een beurs toe hieronder.</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-800/50">
              <tr>
                <th className="px-4 py-2 text-left text-gray-400">Beurs</th>
                <th className="px-4 py-2 text-left text-gray-400">Code</th>
                <th className="px-4 py-2 text-left text-gray-400">Land</th>
                <th className="px-4 py-2 text-right text-gray-400">Symbolen</th>
                <th className="px-4 py-2 text-left text-gray-400">Laatste import</th>
                <th className="px-4 py-2 text-center text-gray-400">Acties</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-800">
              {exchanges.map(ex => (
                <tr key={ex.id} className="hover:bg-gray-800/30">
                  <td className="px-4 py-3 text-white font-medium">{ex.displayName}</td>
                  <td className="px-4 py-3 font-mono text-axon-400">{ex.exchangeCode}</td>
                  <td className="px-4 py-3 text-gray-400">{ex.country}</td>
                  <td className="px-4 py-3 text-right font-mono text-gray-300">{ex.symbolCount.toLocaleString()}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">
                    {ex.lastImportAt ? new Date(ex.lastImportAt).toLocaleString('nl-NL') : 'Nooit'}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        onClick={() => toggleMutation.mutate({ id: ex.id, isEnabled: !ex.isEnabled })}
                        className={`px-2 py-1 rounded text-xs font-medium ${
                          ex.isEnabled
                            ? 'bg-green-500/15 text-green-400'
                            : 'bg-gray-700 text-gray-500'
                        }`}
                      >
                        {ex.isEnabled ? 'Actief' : 'Inactief'}
                      </button>
                      <button
                        onClick={() => handleImport(ex.id)}
                        disabled={importing === ex.id}
                        className="p-1.5 rounded bg-blue-500/15 text-blue-400 hover:bg-blue-500/25 transition-colors disabled:opacity-50"
                        title="Importeer symbolen"
                      >
                        <Download size={14} className={importing === ex.id ? 'animate-bounce' : ''} />
                      </button>
                      <button
                        onClick={() => { if (window.confirm(`Beurs ${ex.exchangeCode} verwijderen inclusief alle symbolen?`)) deleteMutation.mutate(ex.id); }}
                        className="p-1.5 rounded text-gray-600 hover:text-red-400 transition-colors"
                        title="Verwijder beurs"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Add exchange */}
      {available.length > 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
          <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider mb-3">Beurs Toevoegen</h3>
          <div className="flex flex-wrap gap-2">
            {available.map(preset => (
              <button
                key={preset.code}
                onClick={() => addMutation.mutate({
                  exchangeCode: preset.code,
                  displayName: preset.name,
                  country: preset.country,
                })}
                disabled={addMutation.isPending}
                className="px-3 py-2 rounded-lg bg-gray-800 border border-gray-700 text-sm text-gray-300 hover:border-axon-500 hover:text-white transition-all flex items-center gap-2"
              >
                <Plus size={14} />
                <span className="font-mono text-axon-400">{preset.code}</span>
                <span className="text-gray-500">{preset.name}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
