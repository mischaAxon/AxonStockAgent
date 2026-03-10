import { useState } from 'react';
import { RotateCcw, Save, AlertCircle } from 'lucide-react';
import { useAlgoSettings, useUpdateAlgoSetting, useResetAlgoSettings } from '../hooks/useApi';
import type { AlgoSetting } from '../types';

// ─── helpers ────────────────────────────────────────────────────────────────

const CATEGORY_LABELS: Record<string, string> = {
  weights:       'Gewichten',
  thresholds:    'Drempelwaarden',
  scan:          'Scan Instellingen',
  notifications: 'Notificaties',
};

const CATEGORY_ORDER = ['weights', 'thresholds', 'scan', 'notifications'];

function formatKey(key: string): string {
  return key.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}

// ─── WeightsSumBar ───────────────────────────────────────────────────────────

function WeightsSumBar({ settings, pendingValues }: {
  settings: AlgoSetting[];
  pendingValues: Record<number, string>;
}) {
  const sum = settings.reduce((acc, s) => {
    const v = pendingValues[s.id] ?? s.value;
    return acc + (parseFloat(v) || 0);
  }, 0);

  const isOk = Math.abs(sum - 1.0) <= 0.001;

  return (
    <div className={`flex items-center gap-2 text-xs px-3 py-2 rounded-lg mb-4 ${
      isOk ? 'bg-green-500/10 text-green-400' : 'bg-red-500/10 text-red-400'
    }`}>
      <span className="font-semibold">Som gewichten:</span>
      <span>{(sum * 100).toFixed(1)}%</span>
      {isOk ? '✓ Correct (100%)' : '✕ Moet optellen tot 100%'}
    </div>
  );
}

// ─── SettingRow ──────────────────────────────────────────────────────────────

function SettingRow({ setting, onSave }: {
  setting: AlgoSetting;
  onSave: (id: number, value: string) => Promise<string | null>;
}) {
  const [localValue, setLocalValue] = useState(setting.value);
  const [error, setError]           = useState<string | null>(null);
  const [saving, setSaving]         = useState(false);
  const [saved, setSaved]           = useState(false);

  async function handleSave() {
    setSaving(true);
    setError(null);
    const err = await onSave(setting.id, localValue);
    setSaving(false);
    if (err) {
      setError(err);
    } else {
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    }
  }

  const isDirty = localValue !== setting.value;

  return (
    <div className="py-3 border-b border-gray-800 last:border-0">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <p className="text-sm font-medium text-white">{formatKey(setting.key)}</p>
          {setting.description && (
            <p className="text-xs text-gray-500 mt-0.5">{setting.description}</p>
          )}
          {error && (
            <p className="text-xs text-red-400 mt-1 flex items-center gap-1">
              <AlertCircle size={11} />
              {error}
            </p>
          )}
        </div>

        <div className="flex items-center gap-2 flex-shrink-0">
          {setting.valueType === 'boolean' ? (
            <button
              onClick={() => {
                const next = localValue === 'true' ? 'false' : 'true';
                setLocalValue(next);
                onSave(setting.id, next).then(err => err && setError(err));
              }}
              className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
                localValue === 'true' ? 'bg-axon-600' : 'bg-gray-700'
              }`}
            >
              <span className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                localValue === 'true' ? 'translate-x-6' : 'translate-x-1'
              }`} />
            </button>
          ) : (
            <>
              <input
                type="number"
                value={localValue}
                onChange={e => { setLocalValue(e.target.value); setError(null); setSaved(false); }}
                onKeyDown={e => e.key === 'Enter' && handleSave()}
                min={setting.minValue ?? undefined}
                max={setting.maxValue ?? undefined}
                step={setting.valueType === 'integer' ? 1 : 0.01}
                className="w-28 bg-gray-800 border border-gray-700 text-white text-sm rounded-lg px-3 py-1.5 text-right focus:outline-none focus:border-axon-400 transition-colors"
              />
              <button
                onClick={handleSave}
                disabled={!isDirty || saving}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-colors flex items-center gap-1 ${
                  saved
                    ? 'bg-green-500/20 text-green-400'
                    : isDirty
                      ? 'bg-axon-600 hover:bg-axon-500 text-white'
                      : 'bg-gray-800 text-gray-600 cursor-not-allowed'
                }`}
              >
                {saved ? '✓' : <><Save size={11} /> Opslaan</>}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── CategorySection ─────────────────────────────────────────────────────────

function CategorySection({ category, settings, onSave }: {
  category: string;
  settings: AlgoSetting[];
  onSave: (id: number, value: string) => Promise<string | null>;
}) {
  const [pendingValues, setPendingValues] = useState<Record<number, string>>({});

  async function handleSave(id: number, value: string): Promise<string | null> {
    setPendingValues(prev => ({ ...prev, [id]: value }));
    const result = await onSave(id, value);
    if (result) {
      setPendingValues(prev => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
    }
    return result;
  }

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl p-5 mb-4">
      <h2 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">
        {CATEGORY_LABELS[category] ?? category}
      </h2>

      {category === 'weights' && (
        <WeightsSumBar settings={settings} pendingValues={pendingValues} />
      )}

      <div>
        {settings.map(s => (
          <SettingRow key={s.id} setting={s} onSave={handleSave} />
        ))}
      </div>
    </div>
  );
}

// ─── Main Page ───────────────────────────────────────────────────────────────

export default function AdminSettingsPage() {
  const { data, isLoading, error } = useAlgoSettings();
  const updateSetting   = useUpdateAlgoSetting();
  const resetSettings   = useResetAlgoSettings();
  const [resetConfirm, setResetConfirm] = useState(false);

  const grouped = (data as { data?: Record<string, AlgoSetting[]> } | undefined)?.data ?? {};

  async function handleSave(id: number, value: string): Promise<string | null> {
    try {
      await updateSetting.mutateAsync({ id, value });
      return null;
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? (err instanceof Error ? err.message : 'Opslaan mislukt');
      return msg;
    }
  }

  async function handleReset() {
    if (!resetConfirm) {
      setResetConfirm(true);
      setTimeout(() => setResetConfirm(false), 4000);
      return;
    }
    await resetSettings.mutateAsync();
    setResetConfirm(false);
  }

  const orderedCategories = [
    ...CATEGORY_ORDER.filter(c => grouped[c]),
    ...Object.keys(grouped).filter(c => !CATEGORY_ORDER.includes(c)),
  ];

  return (
    <div>
      {/* Header */}
      <div className="flex items-start justify-between mb-8 gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Algoritme Instellingen</h1>
          <p className="text-gray-400 text-sm mt-1">Configureer gewichten, drempelwaarden en scaninstellingen</p>
        </div>
        <button
          onClick={handleReset}
          disabled={resetSettings.isPending}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
            resetConfirm
              ? 'bg-red-600 hover:bg-red-500 text-white'
              : 'bg-gray-800 hover:bg-gray-700 text-gray-300'
          }`}
        >
          <RotateCcw size={14} className={resetSettings.isPending ? 'animate-spin' : ''} />
          {resetConfirm ? 'Klik nogmaals om te bevestigen' : 'Reset naar standaard'}
        </button>
      </div>

      {isLoading && <p className="text-gray-400 text-sm">Laden…</p>}

      {error && (
        <div className="bg-red-500/10 border border-red-500/30 rounded-lg px-4 py-3 text-sm text-red-400">
          Fout bij laden: {(error as Error).message}
        </div>
      )}

      {!isLoading && !error && orderedCategories.length === 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 text-center">
          <p className="text-gray-400 text-sm">Geen instellingen gevonden.</p>
          <p className="text-xs text-gray-600 mt-1">Controleer of de database correct geseeded is.</p>
        </div>
      )}

      {orderedCategories.map(category => (
        <CategorySection
          key={category}
          category={category}
          settings={grouped[category] ?? []}
          onSave={handleSave}
        />
      ))}
    </div>
  );
}
