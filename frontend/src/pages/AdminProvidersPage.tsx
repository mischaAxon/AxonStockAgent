import { useState } from 'react';
import {
  Eye, EyeOff, Plug, RefreshCw,
  CheckCircle2, AlertTriangle, XCircle, HelpCircle,
} from 'lucide-react';
import { useProviders, useUpdateProvider, useTestProvider } from '../hooks/useApi';

interface Provider {
  id: number;
  name: string;
  displayName: string;
  providerType: string;
  isEnabled: boolean;
  rateLimitPerMinute: number;
  supportsEu: boolean;
  supportsUs: boolean;
  isFree: boolean;
  monthlyCost: number;
  healthStatus: string;
  lastHealthCheck: string | null;
  updatedAt: string;
  hasApiKey: boolean;
}

const TYPE_LABELS: Record<string, string> = {
  market_data:   'Marktdata',
  news:          'Nieuws',
  fundamentals:  'Fundamentals',
  all:           'Alles',
};

const TYPE_COLORS: Record<string, string> = {
  market_data:  'bg-blue-500/10 text-blue-400',
  news:         'bg-purple-500/10 text-purple-400',
  fundamentals: 'bg-amber-500/10 text-amber-400',
  all:          'bg-axon-600/20 text-axon-400',
};

const HEALTH: Record<string, { color: string; label: string; Icon: React.FC<{ size?: number; className?: string }> }> = {
  healthy:  { color: 'text-green-400',  label: 'Gezond',   Icon: CheckCircle2  },
  degraded: { color: 'text-orange-400', label: 'Beperkt',  Icon: AlertTriangle },
  down:     { color: 'text-red-400',    label: 'Offline',  Icon: XCircle       },
  unknown:  { color: 'text-gray-500',   label: 'Onbekend', Icon: HelpCircle    },
};

interface TestResult {
  health: string;
  detail: string | null;
  checkedAt: string;
}

function ProviderCard({ provider }: { provider: Provider }) {
  const [apiKey,     setApiKey]     = useState('');
  const [showKey,    setShowKey]    = useState(false);
  const [testResult, setTestResult] = useState<TestResult | null>(null);

  const updateProvider = useUpdateProvider();
  const testProvider   = useTestProvider();

  function handleToggle() {
    updateProvider.mutate({ name: provider.name, isEnabled: !provider.isEnabled });
  }

  function handleSaveApiKey() {
    if (!apiKey.trim()) return;
    updateProvider.mutate({ name: provider.name, apiKey: apiKey.trim() });
    setApiKey('');
  }

  async function handleTest() {
    setTestResult(null);
    try {
      const res = await testProvider.mutateAsync(provider.name);
      setTestResult((res as { data: TestResult }).data);
    } catch {
      setTestResult({ health: 'down', detail: 'Verbinding mislukt', checkedAt: new Date().toISOString() });
    }
  }

  const health     = HEALTH[provider.healthStatus] ?? HEALTH.unknown;
  const HealthIcon = health.Icon;

  return (
    <div className={`bg-gray-900 border rounded-xl p-5 flex flex-col gap-4 transition-opacity ${
      provider.isEnabled ? 'border-gray-700' : 'border-gray-800 opacity-70'
    }`}>

      {/* Header: naam + type badge + toggle */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div className="w-9 h-9 rounded-lg bg-gray-800 flex items-center justify-center flex-shrink-0">
            <Plug size={15} className="text-gray-400" />
          </div>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-white leading-tight">{provider.displayName}</p>
            <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium mt-0.5 ${
              TYPE_COLORS[provider.providerType] ?? TYPE_COLORS.market_data
            }`}>
              {TYPE_LABELS[provider.providerType] ?? provider.providerType}
            </span>
          </div>
        </div>

        {/* Switch toggle */}
        <button
          onClick={handleToggle}
          disabled={updateProvider.isPending}
          aria-label={provider.isEnabled ? 'Uitschakelen' : 'Inschakelen'}
          className={`relative inline-flex h-6 w-11 flex-shrink-0 items-center rounded-full transition-colors disabled:opacity-50 ${
            provider.isEnabled ? 'bg-axon-600' : 'bg-gray-700'
          }`}
        >
          <span className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
            provider.isEnabled ? 'translate-x-6' : 'translate-x-1'
          }`} />
        </button>
      </div>

      {/* API key input */}
      <div>
        <label className="block text-xs text-gray-500 mb-1.5">API Key</label>
        <div className="flex gap-2">
          <div className="relative flex-1">
            <input
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={e => setApiKey(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleSaveApiKey()}
              placeholder={provider.hasApiKey ? '••••••••••••••••' : 'Voer API key in…'}
              className="w-full bg-gray-800 border border-gray-700 text-white rounded-lg pl-3 pr-9 py-2 text-xs focus:outline-none focus:border-axon-400 focus:ring-1 focus:ring-axon-400 transition-colors placeholder-gray-600"
            />
            <button
              type="button"
              onClick={() => setShowKey(v => !v)}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300 transition-colors"
            >
              {showKey ? <EyeOff size={13} /> : <Eye size={13} />}
            </button>
          </div>
          <button
            onClick={handleSaveApiKey}
            disabled={!apiKey.trim() || updateProvider.isPending}
            className="px-3 py-2 bg-axon-600 hover:bg-axon-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs rounded-lg transition-colors whitespace-nowrap"
          >
            Opslaan
          </button>
        </div>
      </div>

      {/* Badges: EU / US / rate limit / prijs */}
      <div className="flex flex-wrap gap-1.5">
        {provider.supportsEu && (
          <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-300">🇪🇺 EU</span>
        )}
        {provider.supportsUs && (
          <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-300">🇺🇸 US</span>
        )}
        <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-400">
          {provider.rateLimitPerMinute}/min
        </span>
        {provider.isFree ? (
          <span className="px-2 py-0.5 rounded text-xs bg-green-500/10 text-green-400">Gratis</span>
        ) : (
          <span className="px-2 py-0.5 rounded text-xs bg-gray-800 text-gray-400">
            €{Number(provider.monthlyCost).toFixed(2)}/mnd
          </span>
        )}
      </div>

      {/* Health status + test knop */}
      <div className="flex items-center justify-between pt-2 border-t border-gray-800">
        <div className="flex items-center gap-1.5">
          <HealthIcon size={13} className={health.color} />
          <span className={`text-xs ${health.color}`}>{health.label}</span>
          {provider.lastHealthCheck && (
            <span className="text-xs text-gray-600">
              &middot;{' '}
              {new Date(provider.lastHealthCheck).toLocaleTimeString('nl-NL', {
                hour: '2-digit', minute: '2-digit',
              })}
            </span>
          )}
        </div>
        <button
          onClick={handleTest}
          disabled={!provider.isEnabled || testProvider.isPending}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-gray-800 hover:bg-gray-700 text-gray-300 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        >
          <RefreshCw size={11} className={testProvider.isPending ? 'animate-spin' : ''} />
          Test verbinding
        </button>
      </div>

      {/* Test resultaat */}
      {testResult && (
        <div className={`rounded-lg px-3 py-2 text-xs ${
          testResult.health === 'healthy'  ? 'bg-green-500/10 text-green-400' :
          testResult.health === 'degraded' ? 'bg-orange-500/10 text-orange-400' :
                                            'bg-red-500/10 text-red-400'
        }`}>
          {testResult.health === 'healthy'
            ? '✓ Verbinding geslaagd'
            : testResult.health === 'degraded'
              ? '⚠ Beperkte verbinding'
              : `✕ Verbinding mislukt${testResult.detail ? `: ${testResult.detail}` : ''}`}
        </div>
      )}
    </div>
  );
}

export default function AdminProvidersPage() {
  const { data, isLoading, error } = useProviders();
  const providers: Provider[] = (data as { data?: Provider[] } | undefined)?.data ?? [];

  const free = providers.filter(p => p.isFree);
  const paid = providers.filter(p => !p.isFree);

  return (
    <div>
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-white">Data Providers</h1>
        <p className="text-gray-400 text-sm mt-1">Beheer externe datadiensten en API keys</p>
      </div>

      {isLoading && <p className="text-gray-400 text-sm">Laden…</p>}

      {error && (
        <div className="bg-red-500/10 border border-red-500/30 rounded-lg px-4 py-3 text-sm text-red-400">
          Fout bij laden: {(error as Error).message}
        </div>
      )}

      {!isLoading && !error && (
        <>
          {free.length > 0 && (
            <section className="mb-8">
              <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-4">Gratis</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {free.map(p => <ProviderCard key={p.name} provider={p} />)}
              </div>
            </section>
          )}

          {paid.length > 0 && (
            <section>
              <h2 className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-4">Betaald</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                {paid.map(p => <ProviderCard key={p.name} provider={p} />)}
              </div>
            </section>
          )}

          {providers.length === 0 && (
            <p className="text-gray-500 text-sm">Geen providers gevonden.</p>
          )}
        </>
      )}
    </div>
  );
}
