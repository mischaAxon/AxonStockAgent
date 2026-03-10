import { useState, useEffect } from 'react';
import { SlidersHorizontal, RotateCcw, Save, Info } from 'lucide-react';
import { useAlgoSettings, useUpdateAlgoSetting, useResetAlgoSettings } from '../hooks/useApi';

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 space-y-4">
      <h2 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">{title}</h2>
      {children}
    </div>
  );
}

function Slider({ label, value, min, max, step = 0.01, onChange, format }: {
  label: string; value: number; min: number; max: number; step?: number;
  onChange: (v: number) => void; format?: (v: number) => string;
}) {
  return (
    <div>
      <div className="flex justify-between mb-1">
        <span className="text-sm text-gray-300">{label}</span>
        <span className="text-sm font-mono text-axon-400">{format ? format(value) : value}</span>
      </div>
      <input
        type="range" min={min} max={max} step={step} value={value}
        onChange={e => onChange(parseFloat(e.target.value))}
        className="w-full accent-axon-500"
      />
    </div>
  );
}

function Toggle({ label, description, value, onChange }: {
  label: string; description: string; value: boolean; onChange: (v: boolean) => void;
}) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <div>
        <div className="text-sm font-medium text-gray-200">{label}</div>
        <div className="text-xs text-gray-500 mt-0.5">{description}</div>
      </div>
      <button
        onClick={() => onChange(!value)}
        className={`relative inline-flex h-6 w-11 flex-shrink-0 rounded-full transition-colors ${value ? 'bg-axon-500' : 'bg-gray-700'}`}
      >
        <span className={`inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform mt-0.5 ${value ? 'translate-x-5' : 'translate-x-0.5'}`} />
      </button>
    </div>
  );
}

export default function AdminSettingsPage() {
  const { data: settings = {}, isLoading } = useAlgoSettings();
  const updateSetting = useUpdateAlgoSetting();
  const resetSettings = useResetAlgoSettings();

  const [weights, setWeights] = useState({ technical: 0.35, ml: 0.25, sentiment: 0.15, claude: 0.25 });
  const [techWeights, setTechWeights] = useState({ trend: 3, momentum: 2, volatility: 1, volume: 2 });
  const [thresholds, setThresholds] = useState({ bull: 0.35, bear: -0.35 });
  const [scan, setScan] = useState({ intervalMinutes: 15, cooldownMinutes: 60, candleHistory: 100, timeframe: 'D' });
  const [features, setFeatures] = useState({ enableMl: true, enableClaude: true, enableSentiment: true, enableNewsFetcher: true });
  const [saved, setSaved] = useState<string | null>(null);

  useEffect(() => {
    if (!settings || Object.keys(settings).length === 0) return;
    if (settings.weights) setWeights(settings.weights as any);
    if (settings.technical_weights) setTechWeights(settings.technical_weights as any);
    if (settings.thresholds) setThresholds(settings.thresholds as any);
    if (settings.scan) setScan(settings.scan as any);
    if (settings.features) setFeatures(settings.features as any);
  }, [settings]);

  const weightsSum = Math.round((weights.technical + weights.ml + weights.sentiment + weights.claude) * 100);
  const weightsValid = weightsSum === 100;

  async function saveKey(key: string, value: unknown) {
    await updateSetting.mutateAsync({ key, value });
    setSaved(key);
    setTimeout(() => setSaved(null), 2000);
  }

  if (isLoading) return <div className="p-6 text-gray-500">Laden...</div>;

  return (
    <div className="p-6 space-y-6 max-w-3xl">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <SlidersHorizontal className="w-6 h-6 text-axon-400" />
          <h1 className="text-2xl font-bold text-white">Algoritme Instellingen</h1>
        </div>
        <button
          onClick={() => resetSettings.mutate()}
          disabled={resetSettings.isPending}
          className="flex items-center gap-2 px-3 py-2 text-sm bg-gray-800 border border-gray-700 text-gray-300 rounded-lg hover:border-red-700 hover:text-red-400 transition-colors"
        >
          <RotateCcw className="w-4 h-4" /> Reset naar standaard
        </button>
      </div>

      {/* Weights */}
      <Section title="Algoritme Gewichten">
        <div className="space-y-3">
          <Slider label="Technisch" value={weights.technical} min={0} max={1} onChange={v => setWeights(w => ({ ...w, technical: Math.round(v * 100) / 100 }))} format={v => `${Math.round(v * 100)}%`} />
          <Slider label="ML Model" value={weights.ml} min={0} max={1} onChange={v => setWeights(w => ({ ...w, ml: Math.round(v * 100) / 100 }))} format={v => `${Math.round(v * 100)}%`} />
          <Slider label="Sentiment" value={weights.sentiment} min={0} max={1} onChange={v => setWeights(w => ({ ...w, sentiment: Math.round(v * 100) / 100 }))} format={v => `${Math.round(v * 100)}%`} />
          <Slider label="Claude AI" value={weights.claude} min={0} max={1} onChange={v => setWeights(w => ({ ...w, claude: Math.round(v * 100) / 100 }))} format={v => `${Math.round(v * 100)}%`} />
        </div>
        {/* Stacked bar */}
        <div className="flex h-3 rounded-full overflow-hidden mt-2">
          <div className="bg-blue-500 transition-all" style={{ width: `${weights.technical * 100}%` }} title="Technisch" />
          <div className="bg-purple-500 transition-all" style={{ width: `${weights.ml * 100}%` }} title="ML" />
          <div className="bg-yellow-500 transition-all" style={{ width: `${weights.sentiment * 100}%` }} title="Sentiment" />
          <div className="bg-axon-500 transition-all" style={{ width: `${weights.claude * 100}%` }} title="Claude" />
        </div>
        <div className="flex gap-4 text-xs text-gray-500 flex-wrap">
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-blue-500 inline-block" /> Technisch</span>
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-purple-500 inline-block" /> ML Model</span>
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-yellow-500 inline-block" /> Sentiment</span>
          <span className="flex items-center gap-1"><span className="w-2 h-2 rounded-full bg-axon-500 inline-block" /> Claude AI</span>
        </div>
        {!weightsValid && (
          <p className="text-xs text-red-400 flex items-center gap-1"><Info className="w-3 h-3" /> Totaal is {weightsSum}%, moet 100% zijn</p>
        )}
        <button
          disabled={!weightsValid || updateSetting.isPending}
          onClick={() => saveKey('weights', weights)}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" /> {saved === 'weights' ? 'Opgeslagen!' : 'Opslaan'}
        </button>
      </Section>

      {/* Technical weights */}
      <Section title="Technische Indicatoren">
        <div className="space-y-3">
          {([['trend', 'Trend', 'Bepaalt het gewicht van trendvolgende signalen (EMA, MACD)'],
            ['momentum', 'Momentum', 'RSI en stochastische oscillator signalen'],
            ['volatility', 'Volatiliteit', 'ATR en Bollinger Band signalen'],
            ['volume', 'Volume', 'OBV en volumebevestiging signalen']] as [keyof typeof techWeights, string, string][]).map(([key, label, desc]) => (
            <div key={key}>
              <Slider
                label={label}
                value={techWeights[key]}
                min={1} max={5} step={1}
                onChange={v => setTechWeights(w => ({ ...w, [key]: v }))}
                format={v => `${v}/5`}
              />
              <p className="text-xs text-gray-600 mt-0.5">{desc}</p>
            </div>
          ))}
        </div>
        <button
          onClick={() => saveKey('technical_weights', techWeights)}
          disabled={updateSetting.isPending}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" /> {saved === 'technical_weights' ? 'Opgeslagen!' : 'Opslaan'}
        </button>
      </Section>

      {/* Thresholds */}
      <Section title="Drempelwaarden">
        <div className="space-y-3">
          <Slider label="Bull drempel" value={thresholds.bull} min={0.1} max={0.9} step={0.05} onChange={v => setThresholds(t => ({ ...t, bull: v }))} format={v => `+${(v * 100).toFixed(0)}%`} />
          <Slider label="Bear drempel" value={thresholds.bear} min={-0.9} max={-0.1} step={0.05} onChange={v => setThresholds(t => ({ ...t, bear: v }))} format={v => `${(v * 100).toFixed(0)}%`} />
        </div>
        {/* Gauge */}
        <div className="relative h-4 bg-gray-800 rounded-full overflow-hidden mt-2">
          <div className="absolute inset-0 flex">
            <div className="bg-red-900/60" style={{ width: `${((thresholds.bear + 1) / 2) * 100}%` }} />
            <div className="bg-gray-700 flex-1" />
          </div>
          <div className="absolute inset-0 flex">
            <div style={{ width: `${((thresholds.bull + 1) / 2) * 100}%` }} />
            <div className="bg-green-900/60 flex-1" />
          </div>
          <div className="absolute inset-y-0 w-0.5 bg-red-500" style={{ left: `${((thresholds.bear + 1) / 2) * 100}%` }} />
          <div className="absolute inset-y-0 w-0.5 bg-green-500" style={{ left: `${((thresholds.bull + 1) / 2) * 100}%` }} />
          <div className="absolute inset-y-0 w-0.5 bg-gray-400" style={{ left: '50%' }} />
        </div>
        <div className="flex justify-between text-xs text-gray-500">
          <span className="text-red-400">Bear {(thresholds.bear * 100).toFixed(0)}%</span>
          <span>Neutraal</span>
          <span className="text-green-400">Bull +{(thresholds.bull * 100).toFixed(0)}%</span>
        </div>
        <button
          onClick={() => saveKey('thresholds', thresholds)}
          disabled={updateSetting.isPending}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" /> {saved === 'thresholds' ? 'Opgeslagen!' : 'Opslaan'}
        </button>
      </Section>

      {/* Scan config */}
      <Section title="Scanner Configuratie">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="text-xs text-gray-400 block mb-1">Scan interval</label>
            <select value={scan.intervalMinutes} onChange={e => setScan(s => ({ ...s, intervalMinutes: parseInt(e.target.value) }))}
              className="w-full bg-gray-800 border border-gray-700 text-gray-200 text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-axon-500">
              {[5, 10, 15, 30, 60].map(v => <option key={v} value={v}>{v} min</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-400 block mb-1">Cooldown</label>
            <select value={scan.cooldownMinutes} onChange={e => setScan(s => ({ ...s, cooldownMinutes: parseInt(e.target.value) }))}
              className="w-full bg-gray-800 border border-gray-700 text-gray-200 text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-axon-500">
              {[15, 30, 60, 120].map(v => <option key={v} value={v}>{v} min</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-400 block mb-1">Timeframe</label>
            <select value={scan.timeframe} onChange={e => setScan(s => ({ ...s, timeframe: e.target.value }))}
              className="w-full bg-gray-800 border border-gray-700 text-gray-200 text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-axon-500">
              {[['D', 'Dagelijks'], ['60', '1 uur'], ['W', 'Wekelijks']].map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs text-gray-400 block mb-1">Candle history</label>
            <input type="number" min={50} max={500} value={scan.candleHistory}
              onChange={e => setScan(s => ({ ...s, candleHistory: parseInt(e.target.value) || 100 }))}
              className="w-full bg-gray-800 border border-gray-700 text-gray-200 text-sm rounded-lg px-3 py-2 focus:outline-none focus:border-axon-500" />
          </div>
        </div>
        <button
          onClick={() => saveKey('scan', scan)}
          disabled={updateSetting.isPending}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" /> {saved === 'scan' ? 'Opgeslagen!' : 'Opslaan'}
        </button>
      </Section>

      {/* Feature flags */}
      <Section title="Feature Toggles">
        <div className="divide-y divide-gray-800">
          <Toggle label="ML Model" description="Gebruikt ML.NET FastTree model voor patroonherkenning in koersdata" value={features.enableMl} onChange={v => setFeatures(f => ({ ...f, enableMl: v }))} />
          <Toggle label="Claude AI" description="Stuurt signalen naar Claude API voor diepgaande analyse en aanbevelingen" value={features.enableClaude} onChange={v => setFeatures(f => ({ ...f, enableClaude: v }))} />
          <Toggle label="Sentiment Analyse" description="Verwerkt nieuwssentiment als factor in het signaalalgoritme" value={features.enableSentiment} onChange={v => setFeatures(f => ({ ...f, enableSentiment: v }))} />
          <Toggle label="Nieuws Fetcher" description="Haalt elke 60 seconden nieuws op via actieve providers" value={features.enableNewsFetcher} onChange={v => setFeatures(f => ({ ...f, enableNewsFetcher: v }))} />
        </div>
        <button
          onClick={() => saveKey('features', features)}
          disabled={updateSetting.isPending}
          className="flex items-center gap-2 px-4 py-2 text-sm bg-axon-600 hover:bg-axon-500 disabled:opacity-50 text-white rounded-lg transition-colors"
        >
          <Save className="w-4 h-4" /> {saved === 'features' ? 'Opgeslagen!' : 'Opslaan'}
        </button>
      </Section>
    </div>
  );
}
