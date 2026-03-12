# Prompt 16 — Markets Redesign: Hoofdpagina met horizontale beurs-kolommen

## Doel

De Markets-pagina wordt de centrale hoofdpagina van de app. Watchlist- en Portfolio-pagina's worden verwijderd. De nieuwe Markets-pagina toont alle aandelen als cards in horizontale, scrollbare beurs-kolommen, met een filterbar (BUY/SELL/SQUEEZE/ALL) en zoekbalk met dropdown bovenaan.

## Verificatie achteraf

```bash
cd frontend
npx tsc --noEmit
npm run build
```

Backend:
```bash
cd src/AxonStockAgent.Api
dotnet build --nologo -v quiet
```

---

## Stap 1: Backend — Breid `latest-per-symbol` endpoint uit

Het endpoint `GET /api/v1/signals/latest-per-symbol` retourneert nu alleen `Symbol, FinalVerdict, FinalScore, Direction, CreatedAt`. We hebben ook de status-indicatoren nodig.

Open `src/AxonStockAgent.Api/Controllers/SignalsController.cs`.

Vervang de bestaande `GetLatestPerSymbol` method door:

```csharp
[HttpGet("latest-per-symbol")]
public async Task<IActionResult> GetLatestPerSymbol([FromQuery] int days = 7)
{
    var since = DateTime.UtcNow.AddDays(-Math.Min(days, 90));

    var latestSignals = await _db.Signals
        .Where(s => s.CreatedAt >= since)
        .GroupBy(s => s.Symbol)
        .Select(g => g.OrderByDescending(s => s.CreatedAt).First())
        .Select(s => new
        {
            s.Symbol,
            s.FinalVerdict,
            s.FinalScore,
            s.Direction,
            s.CreatedAt,
            s.TrendStatus,
            s.MomentumStatus,
            s.VolatilityStatus,
            s.VolumeStatus,
            s.TechScore,
            s.SentimentScore,
            s.ClaudeConfidence,
            s.ClaudeDirection
        })
        .ToListAsync();

    return Ok(new { data = latestSignals });
}
```

## Stap 2: Frontend — Update het TypeScript type

Open `frontend/src/types/index.ts`.

Vervang het bestaande `LatestSignalPerSymbol` type door:

```ts
export interface LatestSignalPerSymbol {
  symbol: string;
  finalVerdict: string;
  finalScore: number;
  direction: string;
  createdAt: string;
  trendStatus: string | null;
  momentumStatus: string | null;
  volatilityStatus: string | null;
  volumeStatus: string | null;
  techScore: number;
  sentimentScore: number | null;
  claudeConfidence: number | null;
  claudeDirection: string | null;
}
```

---

## Stap 3: Verwijder Watchlist- en Portfolio-pagina's uit navigatie en routes

### 3a. `frontend/src/components/layout/Layout.tsx`

Verwijder `Eye` en `Briefcase` uit de lucide-react import.

Vervang het hele `navItems` array door:

```tsx
const navItems = [
  { to: '/',          label: 'Markets',   icon: Globe,           end: true  },
  { to: '/signals',   label: 'Signalen',  icon: Activity,        end: false },
  { to: '/sectors',   label: 'Sectoren',  icon: Grid2x2,         end: false },
  { to: '/news',      label: 'Nieuws',    icon: Newspaper,       end: false },
];
```

Verwijder ook de `LayoutDashboard` import als die niet meer nodig is.

### 3b. `frontend/src/App.tsx`

Verwijder de imports van:
- `WatchlistPage`
- `PortfolioPage`
- `DashboardPage`

Verwijder de routes:
- `<Route path="/" element={<DashboardPage />} />`
- `<Route path="/watchlist" element={<WatchlistPage />} />`
- `<Route path="/portfolio" element={<PortfolioPage />} />`

Voeg toe / wijzig:
```tsx
<Route path="/" element={<MarketsPage />} />
```

Verwijder de route `<Route path="/markets" ...>` (die is nu `/`).

Eindresultaat van de protected routes:
```tsx
<Route path="/"          element={<MarketsPage />} />
<Route path="/signals"   element={<SignalsPage />} />
<Route path="/sectors"   element={<SectorsPage />} />
<Route path="/news"      element={<NewsPage />} />
<Route path="/stock/:symbol" element={<StockDetailPage />} />
```

### 3c. Verwijder de bestanden (optioneel, mag ook blijven)

De volgende bestanden worden niet meer gerefereerd en mogen verwijderd:
- `frontend/src/pages/WatchlistPage.tsx`
- `frontend/src/pages/PortfolioPage.tsx`
- `frontend/src/pages/DashboardPage.tsx`

---

## Stap 4: Herschrijf `frontend/src/pages/MarketsPage.tsx` volledig

Vervang de **volledige inhoud** van `frontend/src/pages/MarketsPage.tsx` door het volgende:

```tsx
import { useState, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, TrendingUp, TrendingDown, Minus, ChevronLeft, ChevronRight } from 'lucide-react';
import { useAllSymbols, useBatchQuotes, useLatestSignalsPerSymbol } from '../hooks/useApi';
import type { MarketSymbol, Quote, LatestSignalPerSymbol } from '../types';

// ── helpers ──────────────────────────────────────────────────────────────────

function countryFlag(code: string | null): string {
  if (!code || code.length !== 2) return '\uD83C\uDF10';
  return code.toUpperCase().replace(/./g, c =>
    String.fromCodePoint(c.charCodeAt(0) + 127397));
}

function formatPrice(price: number): string {
  return price >= 1000
    ? price.toLocaleString('nl-NL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    : price.toFixed(2);
}

type VerdictFilter = '' | 'BUY' | 'SELL' | 'SQUEEZE';

// ── SymbolSearchDropdown ─────────────────────────────────────────────────────

function SymbolSearchDropdown({
  symbols,
  onSelect,
}: {
  symbols: MarketSymbol[];
  onSelect: (symbol: string) => void;
}) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const filtered = useMemo(() => {
    if (!query.trim()) return [];
    const q = query.toLowerCase();
    return symbols
      .filter(s =>
        s.symbol.toLowerCase().includes(q) ||
        (s.name?.toLowerCase().includes(q))
      )
      .slice(0, 8);
  }, [symbols, query]);

  return (
    <div ref={ref} className="relative">
      <div className="relative">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
        <input
          type="text"
          value={query}
          onChange={e => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => { if (query.trim()) setOpen(true); }}
          onBlur={() => setTimeout(() => setOpen(false), 200)}
          placeholder="Zoek symbool of bedrijf..."
          className="w-72 pl-9 pr-4 py-2 rounded-lg bg-gray-900 border border-gray-800 text-sm text-white placeholder-gray-600 focus:border-axon-500 focus:outline-none transition-colors"
        />
      </div>
      {open && filtered.length > 0 && (
        <div className="absolute z-50 mt-1 w-full bg-gray-900 border border-gray-700 rounded-lg shadow-xl overflow-hidden">
          {filtered.map(s => (
            <button
              key={s.symbol}
              onMouseDown={() => {
                onSelect(s.symbol);
                setQuery('');
                setOpen(false);
              }}
              className="w-full flex items-center gap-3 px-3 py-2 hover:bg-gray-800 transition-colors text-left"
            >
              {s.logo ? (
                <img src={s.logo} alt={s.symbol} className="w-6 h-6 rounded object-contain bg-white/5 p-0.5" onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }} />
              ) : (
                <div className="w-6 h-6 rounded bg-gray-800 flex items-center justify-center">
                  <span className="text-[9px] text-gray-500 font-mono">{s.symbol.slice(0, 2)}</span>
                </div>
              )}
              <div className="min-w-0">
                <span className="font-mono text-sm font-semibold text-white">{s.symbol}</span>
                {s.name && <span className="text-xs text-gray-500 ml-2 truncate">{s.name}</span>}
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ── StatusBadge ──────────────────────────────────────────────────────────────

function StatusBadge({ label, color }: { label: string; color: string }) {
  return (
    <span className={`px-1.5 py-0.5 rounded text-[9px] font-medium ${color}`}>
      {label}
    </span>
  );
}

// ── StockCard ────────────────────────────────────────────────────────────────

function StockCard({
  symbol,
  quote,
  signal,
  onClick,
}: {
  symbol: MarketSymbol;
  quote: Quote | undefined;
  signal: LatestSignalPerSymbol | undefined;
  onClick: () => void;
}) {
  const verdictColors: Record<string, string> = {
    BUY: 'border-green-500/40',
    SELL: 'border-red-500/40',
    SQUEEZE: 'border-yellow-500/40',
  };
  const borderColor = signal ? verdictColors[signal.finalVerdict] ?? 'border-gray-800' : 'border-gray-800';

  return (
    <div
      onClick={onClick}
      className={`bg-gray-900 border ${borderColor} rounded-xl p-3.5 cursor-pointer hover:bg-gray-800/60 transition-all group min-w-[220px] w-[220px] flex-shrink-0`}
    >
      {/* Header: logo + symbol + name */}
      <div className="flex items-center gap-2.5 mb-2.5">
        {symbol.logo ? (
          <img
            src={symbol.logo}
            alt={symbol.symbol}
            className="w-8 h-8 rounded-lg object-contain bg-white/5 p-0.5 flex-shrink-0"
            onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }}
          />
        ) : (
          <div className="w-8 h-8 rounded-lg bg-gray-800 flex items-center justify-center flex-shrink-0">
            <span className="font-mono text-[10px] text-gray-500">{symbol.symbol.slice(0, 3)}</span>
          </div>
        )}
        <div className="min-w-0">
          <p className="font-mono text-sm font-bold text-white group-hover:text-axon-400 transition-colors leading-tight">
            {symbol.symbol}
          </p>
          {symbol.name && (
            <p className="text-[11px] text-gray-500 truncate leading-tight">{symbol.name}</p>
          )}
        </div>
      </div>

      {/* Price + Change */}
      {quote ? (
        <div className="flex items-baseline justify-between mb-2.5">
          <span className="font-mono text-lg font-bold text-white">
            {formatPrice(quote.currentPrice)}
          </span>
          <span className={`flex items-center gap-0.5 text-xs font-semibold ${
            quote.changePercent > 0 ? 'text-green-400'
              : quote.changePercent < 0 ? 'text-red-400'
              : 'text-gray-500'
          }`}>
            {quote.changePercent > 0 ? <TrendingUp size={11} /> : quote.changePercent < 0 ? <TrendingDown size={11} /> : <Minus size={11} />}
            {quote.changePercent > 0 ? '+' : ''}{quote.changePercent.toFixed(2)}%
          </span>
        </div>
      ) : (
        <div className="h-8 mb-2.5 flex items-center">
          <span className="text-gray-700 text-xs">Laden...</span>
        </div>
      )}

      {/* Sector tag */}
      {symbol.sector && (
        <div className="mb-2">
          <span className="px-1.5 py-0.5 rounded text-[10px] bg-gray-800 text-gray-400">
            {symbol.sector}
          </span>
        </div>
      )}

      {/* Signal verdict + score */}
      {signal && (
        <div className="mb-2">
          <div className="flex items-center gap-2">
            <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
              signal.finalVerdict === 'BUY' ? 'bg-green-500/20 text-green-400'
                : signal.finalVerdict === 'SELL' ? 'bg-red-500/20 text-red-400'
                : 'bg-yellow-500/20 text-yellow-400'
            }`}>
              {signal.finalVerdict}
            </span>
            {/* Mini score bar */}
            <div className="flex items-center gap-1.5 flex-1">
              <div className="flex-1 h-1 bg-gray-700 rounded-full overflow-hidden">
                <div
                  className={`h-full rounded-full ${
                    signal.finalScore >= 0.6 ? 'bg-green-500'
                      : signal.finalScore >= 0.3 ? 'bg-amber-500'
                      : 'bg-red-500'
                  }`}
                  style={{ width: `${Math.round(signal.finalScore * 100)}%` }}
                />
              </div>
              <span className="font-mono text-[10px] text-gray-400">
                {Math.round(signal.finalScore * 100)}%
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Status badges: trend, momentum, volatility, volume */}
      {signal && (signal.trendStatus || signal.momentumStatus || signal.volatilityStatus || signal.volumeStatus) && (
        <div className="flex flex-wrap gap-1">
          {signal.trendStatus && <StatusBadge label={signal.trendStatus} color="bg-blue-500/15 text-blue-400" />}
          {signal.momentumStatus && <StatusBadge label={signal.momentumStatus} color="bg-purple-500/15 text-purple-400" />}
          {signal.volatilityStatus && <StatusBadge label={signal.volatilityStatus} color="bg-amber-500/15 text-amber-400" />}
          {signal.volumeStatus && <StatusBadge label={signal.volumeStatus} color="bg-gray-600/40 text-gray-300" />}
        </div>
      )}
    </div>
  );
}

// ── ScrollableRow ────────────────────────────────────────────────────────────

function ScrollableRow({ children }: { children: React.ReactNode }) {
  const scrollRef = useRef<HTMLDivElement>(null);

  const scroll = (dir: 'left' | 'right') => {
    if (!scrollRef.current) return;
    const amount = 460; // ~2 cards
    scrollRef.current.scrollBy({ left: dir === 'left' ? -amount : amount, behavior: 'smooth' });
  };

  return (
    <div className="relative group/scroll">
      {/* Left arrow */}
      <button
        onClick={() => scroll('left')}
        className="absolute left-0 top-1/2 -translate-y-1/2 z-10 w-8 h-8 rounded-full bg-gray-800/90 border border-gray-700 flex items-center justify-center text-gray-400 hover:text-white hover:bg-gray-700 transition-all opacity-0 group-hover/scroll:opacity-100"
      >
        <ChevronLeft size={16} />
      </button>

      {/* Scrollable container */}
      <div
        ref={scrollRef}
        className="flex gap-3 overflow-x-auto pb-2 scrollbar-thin scrollbar-thumb-gray-700 scrollbar-track-transparent"
        style={{ scrollbarWidth: 'thin' }}
      >
        {children}
      </div>

      {/* Right arrow */}
      <button
        onClick={() => scroll('right')}
        className="absolute right-0 top-1/2 -translate-y-1/2 z-10 w-8 h-8 rounded-full bg-gray-800/90 border border-gray-700 flex items-center justify-center text-gray-400 hover:text-white hover:bg-gray-700 transition-all opacity-0 group-hover/scroll:opacity-100"
      >
        <ChevronRight size={16} />
      </button>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function MarketsPage() {
  const [search, setSearch] = useState('');
  const [verdictFilter, setVerdictFilter] = useState<VerdictFilter>('');

  const { data: symbolsData, isLoading } = useAllSymbols();
  const allSymbols: MarketSymbol[] = symbolsData?.data ?? [];

  const symbolTickers = useMemo(() => allSymbols.map(s => s.symbol), [allSymbols]);
  const { data: quotesData } = useBatchQuotes(symbolTickers);
  const quotes: Record<string, Quote> = quotesData?.data ?? {};

  const { data: signalsData } = useLatestSignalsPerSymbol(7);
  const signalMap = useMemo(() => {
    const map: Record<string, LatestSignalPerSymbol> = {};
    for (const sig of signalsData?.data ?? []) {
      map[sig.symbol] = sig;
    }
    return map;
  }, [signalsData]);

  const navigate = useNavigate();

  // Apply filters
  const filtered = useMemo(() => {
    let result = allSymbols;

    // Text search
    if (search.trim()) {
      const q = search.toLowerCase();
      result = result.filter(s =>
        s.symbol.toLowerCase().includes(q) ||
        (s.name?.toLowerCase().includes(q)) ||
        (s.sector?.toLowerCase().includes(q))
      );
    }

    // Verdict filter
    if (verdictFilter) {
      result = result.filter(s => {
        const sig = signalMap[s.symbol];
        return sig?.finalVerdict === verdictFilter;
      });
    }

    return result;
  }, [allSymbols, search, verdictFilter, signalMap]);

  // Group by exchange
  const exchangeGroups = useMemo(() => {
    const byExchange: Record<string, { symbols: MarketSymbol[]; country: string }> = {};

    for (const sym of filtered) {
      const exchange = sym.exchange ?? 'Unknown';
      if (!byExchange[exchange]) {
        byExchange[exchange] = { symbols: [], country: sym.country ?? 'XX' };
      }
      byExchange[exchange].symbols.push(sym);
    }

    // Sort: NL exchanges first, then US, then rest alphabetically
    return Object.entries(byExchange)
      .sort(([, a], [, b]) => {
        if (a.country === 'NL' && b.country !== 'NL') return -1;
        if (b.country === 'NL' && a.country !== 'NL') return 1;
        if (a.country === 'US' && b.country !== 'US') return -1;
        if (b.country === 'US' && a.country !== 'US') return 1;
        return 0;
      })
      .map(([exchange, { symbols, country }]) => ({
        exchange,
        country,
        symbols: symbols.sort((a, b) => a.symbol.localeCompare(b.symbol)),
      }));
  }, [filtered]);

  // Counts for filter bar
  const counts = useMemo(() => {
    const c = { all: allSymbols.length, BUY: 0, SELL: 0, SQUEEZE: 0 };
    for (const sym of allSymbols) {
      const sig = signalMap[sym.symbol];
      if (sig?.finalVerdict === 'BUY') c.BUY++;
      else if (sig?.finalVerdict === 'SELL') c.SELL++;
      else if (sig?.finalVerdict === 'SQUEEZE') c.SQUEEZE++;
    }
    return c;
  }, [allSymbols, signalMap]);

  return (
    <div className="space-y-6">
      {/* Header + Search */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Markets</h1>
          <p className="text-sm text-gray-500 mt-1">
            {allSymbols.length} symbolen · {Object.keys(quotes).length} live quotes
          </p>
        </div>

        <SymbolSearchDropdown
          symbols={allSymbols}
          onSelect={symbol => navigate(`/stock/${symbol}`)}
        />
      </div>

      {/* Filter bar */}
      <div className="flex items-center gap-2">
        {([
          { key: '' as VerdictFilter, label: 'Alle', count: counts.all, color: 'bg-gray-800 text-gray-300' },
          { key: 'BUY' as VerdictFilter, label: 'BUY', count: counts.BUY, color: 'bg-green-500/15 text-green-400' },
          { key: 'SELL' as VerdictFilter, label: 'SELL', count: counts.SELL, color: 'bg-red-500/15 text-red-400' },
          { key: 'SQUEEZE' as VerdictFilter, label: 'SQUEEZE', count: counts.SQUEEZE, color: 'bg-yellow-500/15 text-yellow-400' },
        ]).map(({ key, label, count, color }) => (
          <button
            key={key}
            onClick={() => setVerdictFilter(key)}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all flex items-center gap-2 ${
              verdictFilter === key
                ? 'ring-2 ring-axon-500 ' + color
                : color + ' opacity-60 hover:opacity-100'
            }`}
          >
            {label}
            <span className="text-xs opacity-70">{count}</span>
          </button>
        ))}

        {/* Quick text filter */}
        {search && (
          <div className="flex items-center gap-2 ml-4">
            <span className="text-xs text-gray-500">Filter:</span>
            <span className="text-xs text-axon-400 font-mono">\"{search}\"</span>
            <button
              onClick={() => setSearch('')}
              className="text-xs text-gray-500 hover:text-white"
            >
              ×
            </button>
          </div>
        )}
      </div>

      {/* Exchange columns */}
      {isLoading ? (
        <div className="space-y-6">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i}>
              <div className="h-6 bg-gray-800 rounded w-40 mb-3 animate-pulse" />
              <div className="flex gap-3">
                {Array.from({ length: 4 }).map((_, j) => (
                  <div key={j} className="w-[220px] h-[180px] bg-gray-900 border border-gray-800 rounded-xl animate-pulse flex-shrink-0" />
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : exchangeGroups.length === 0 ? (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-12 text-center">
          <p className="text-gray-400">
            {search || verdictFilter
              ? 'Geen aandelen gevonden met de huidige filters.'
              : 'Nog geen symbolen. Voeg symbolen toe via de admin.'}
          </p>
        </div>
      ) : (
        <div className="space-y-8">
          {exchangeGroups.map(({ exchange, country, symbols }) => (
            <div key={exchange}>
              {/* Exchange header */}
              <div className="flex items-center gap-2 mb-3">
                <span className="text-lg">{countryFlag(country)}</span>
                <h2 className="text-base font-semibold text-white">{exchange}</h2>
                <span className="text-xs text-gray-600">{symbols.length} aandelen</span>
              </div>

              {/* Scrollable horizontal card row */}
              <ScrollableRow>
                {symbols.map(sym => (
                  <StockCard
                    key={sym.symbol}
                    symbol={sym}
                    quote={quotes[sym.symbol]}
                    signal={signalMap[sym.symbol]}
                    onClick={() => navigate(`/stock/${sym.symbol}`)}
                  />
                ))}
              </ScrollableRow>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
```

---

## Stap 5: Fix `StockDetailPage.tsx` — back-link

De StockDetailPage heeft een "Terug naar Watchlist" link. Verander die naar Markets.

Open `frontend/src/pages/StockDetailPage.tsx`.

Zoek:
```tsx
<Link to="/watchlist" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white transition-colors">
  <ArrowLeft size={14} /> Terug naar Watchlist
</Link>
```

Vervang door:
```tsx
<Link to="/" className="inline-flex items-center gap-1.5 text-sm text-gray-400 hover:text-white transition-colors">
  <ArrowLeft size={14} /> Terug naar Markets
</Link>
```

---

## Stap 6: Voeg CSS toe voor scrollbar styling (optioneel maar mooi)

Open `frontend/src/index.css` en voeg toe na de bestaande Tailwind imports:

```css
/* Custom scrollbar for horizontal scroll */
.scrollbar-thin::-webkit-scrollbar {
  height: 4px;
}
.scrollbar-thin::-webkit-scrollbar-track {
  background: transparent;
}
.scrollbar-thin::-webkit-scrollbar-thumb {
  background: #374151;
  border-radius: 9999px;
}
.scrollbar-thin::-webkit-scrollbar-thumb:hover {
  background: #4b5563;
}
```

---

## Samenvatting gewijzigde/nieuwe bestanden

| Bestand | Actie |
|---------|-------|
| `src/AxonStockAgent.Api/Controllers/SignalsController.cs` | **Gewijzigd** — uitgebreide `latest-per-symbol` response |
| `frontend/src/types/index.ts` | **Gewijzigd** — `LatestSignalPerSymbol` uitgebreid |
| `frontend/src/components/layout/Layout.tsx` | **Gewijzigd** — watchlist + portfolio uit nav |
| `frontend/src/App.tsx` | **Gewijzigd** — routes opgeschoond, `/` = Markets |
| `frontend/src/pages/MarketsPage.tsx` | **Herschreven** — volledig nieuw design |
| `frontend/src/pages/StockDetailPage.tsx` | **Gewijzigd** — back-link naar Markets |
| `frontend/src/index.css` | **Gewijzigd** — scrollbar styling |
| `frontend/src/pages/WatchlistPage.tsx` | **Verwijderd** |
| `frontend/src/pages/PortfolioPage.tsx` | **Verwijderd** |
| `frontend/src/pages/DashboardPage.tsx` | **Verwijderd** |
