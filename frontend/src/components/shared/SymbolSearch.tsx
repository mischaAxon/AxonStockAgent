import { useState, useRef, useEffect } from 'react';
import { Search, X } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../services/api';

interface SearchResult {
  symbol: string;
  description: string;
  exchange: string;
  type: string;
}

interface Props {
  value: string;
  onChange: (symbol: string, description?: string) => void;
  placeholder?: string;
  className?: string;
}

export default function SymbolSearch({ value, onChange, placeholder = 'Zoek symbool of bedrijf…', className = '' }: Props) {
  const [query, setQuery]     = useState(value);
  const [open, setOpen]       = useState(false);
  const containerRef          = useRef<HTMLDivElement>(null);

  // Sync external value changes
  useEffect(() => { setQuery(value); }, [value]);

  const { data, isFetching } = useQuery({
    queryKey: ['symbol-search', query],
    queryFn: () => api.get<{ data: SearchResult[] }>(`/v1/symbols/search?q=${encodeURIComponent(query)}`),
    enabled: query.length >= 1,
    staleTime: 30_000,
  });

  const results = data?.data ?? [];

  // Close on outside click
  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node))
        setOpen(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, []);

  function handleSelect(r: SearchResult) {
    setQuery(r.symbol);
    onChange(r.symbol, r.description);
    setOpen(false);
  }

  function handleClear() {
    setQuery('');
    onChange('');
    setOpen(false);
  }

  return (
    <div ref={containerRef} className={`relative ${className}`}>
      <div className="relative">
        <Search size={13} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
        <input
          type="text"
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => query.length >= 1 && setOpen(true)}
          onKeyDown={e => {
            if (e.key === 'Escape') setOpen(false);
            if (e.key === 'Enter' && results.length === 0) {
              // Vrij invullen als geen resultaten
              onChange(query.trim().toUpperCase());
              setOpen(false);
            }
          }}
          placeholder={placeholder}
          className="w-full bg-gray-800 border border-gray-700 text-white rounded-lg pl-8 pr-8 py-2 text-sm focus:outline-none focus:border-axon-400 focus:ring-1 focus:ring-axon-400 transition-colors placeholder-gray-600"
        />
        {query && (
          <button
            type="button"
            onClick={handleClear}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300 transition-colors"
          >
            <X size={13} />
          </button>
        )}
      </div>

      {open && query.length >= 1 && (
        <div className="absolute z-50 mt-1 w-full bg-gray-900 border border-gray-700 rounded-lg shadow-xl overflow-hidden">
          {isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-gray-500">Zoeken…</div>
          )}

          {!isFetching && results.length === 0 && (
            <div className="px-3 py-2 text-xs text-gray-500">
              Geen resultaten — druk Enter om "{query.toUpperCase()}" direct te gebruiken
            </div>
          )}

          {results.map(r => (
            <button
              key={r.symbol}
              type="button"
              onMouseDown={e => { e.preventDefault(); handleSelect(r); }}
              className="w-full flex items-center gap-3 px-3 py-2.5 hover:bg-gray-800 transition-colors text-left"
            >
              <div className="flex-1 min-w-0">
                <span className="font-mono font-semibold text-white text-sm">{r.symbol}</span>
                {r.description && (
                  <span className="ml-2 text-xs text-gray-400 truncate">{r.description}</span>
                )}
              </div>
              {r.exchange && (
                <span className="text-xs text-gray-600 flex-shrink-0">{r.exchange}</span>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
