# Prompt 20 — Index Import Fallback: Vul index vanuit beurs-symbolen

## Probleem

EODHD fundamentals API (nodig voor index-componenten) vereist een hoger abonnement. De `GetIndexComponents` call retourneert `Forbidden`, waardoor index-import 0 componenten oplevert. Er is geen feedback in de UI.

## Oplossing

1. Backend: nieuw endpoint `POST /v1/admin/indices/{id}/fill-from-exchange` dat alle MarketSymbols van de gekoppelde exchange als index-leden toevoegt
2. Frontend: feedback na import (succes/fout melding) + "Vul van beurs" knop als alternatief

## Verificatie

```bash
cd src/AxonStockAgent.Api && dotnet build --nologo -v quiet
cd frontend && npx tsc --noEmit && npm run build
```

---

## Stap 1: Backend — fallback endpoint

Open `src/AxonStockAgent.Api/Controllers/AdminController.cs`.

Voeg toe na het bestaande `ImportIndexComponents` endpoint:

```csharp
/// <summary>
/// Fallback: vul een index met ALLE symbolen van de gekoppelde exchange.
/// Gebruik dit als EODHD fundamentals API niet beschikbaar is.
/// </summary>
[HttpPost("indices/{id:int}/fill-from-exchange")]
public async Task<IActionResult> FillIndexFromExchange(int id)
{
    var index = await _db.MarketIndices.FindAsync(id);
    if (index == null) return NotFound();

    if (string.IsNullOrEmpty(index.ExchangeCode))
        return BadRequest(new { error = "Geen exchange gekoppeld aan deze index" });

    // Haal alle actieve symbolen van deze exchange op
    var exchangeSymbols = await _db.MarketSymbols
        .Where(m => m.Exchange == index.ExchangeCode && m.IsActive)
        .ToListAsync();

    if (exchangeSymbols.Count == 0)
        return BadRequest(new { error = $"Geen symbolen gevonden voor exchange '{index.ExchangeCode}'. Importeer eerst de beurs." });

    var now = DateTime.UtcNow;

    // Verwijder bestaande memberships
    var existing = await _db.IndexMemberships
        .Where(m => m.MarketIndexId == id)
        .ToListAsync();
    _db.IndexMemberships.RemoveRange(existing);

    // Voeg alle exchange-symbolen toe als leden
    foreach (var sym in exchangeSymbols)
    {
        _db.IndexMemberships.Add(new IndexMembershipEntity
        {
            MarketIndexId = id,
            Symbol        = sym.Symbol,
            Name          = sym.Name,
            Sector        = sym.Sector,
            Industry      = sym.Industry,
            AddedAt       = now,
        });
    }

    index.SymbolCount = exchangeSymbols.Count;
    index.LastImportAt = now;
    await _db.SaveChangesAsync();

    return Ok(new { data = new { index = index.DisplayName, importedCount = exchangeSymbols.Count, source = "exchange" } });
}
```

---

## Stap 2: Frontend — feedback + fallback knop

Open `frontend/src/pages/AdminExchangesPage.tsx`.

### 2a. Voeg state toe voor feedback

Voeg toe bij de state declarations bovenin de component:

```tsx
const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
```

### 2b. Pas `handleImportIndex` aan met feedback

Vervang de bestaande `handleImportIndex` functie door:

```tsx
async function handleImportIndex(id: number) {
  setImportingIndex(id);
  setFeedback(null);
  try {
    const result: any = await api.post(`/v1/admin/indices/${id}/import`, {});
    const count = result?.data?.importedCount ?? 0;
    if (count > 0) {
      setFeedback({ type: 'success', message: `${count} componenten ge\u00efmporteerd via EODHD` });
    } else {
      setFeedback({ type: 'error', message: 'EODHD retourneerde 0 componenten. Mogelijk vereist dit een hoger abonnement. Gebruik "Vul van beurs" als alternatief.' });
    }
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  } catch (err: any) {
    setFeedback({ type: 'error', message: `Import mislukt: ${err?.message ?? 'Onbekende fout'}` });
  } finally {
    setImportingIndex(null);
  }
}
```

### 2c. Voeg `handleFillFromExchange` functie toe

```tsx
const [fillingIndex, setFillingIndex] = useState<number | null>(null);

async function handleFillFromExchange(id: number) {
  setFillingIndex(id);
  setFeedback(null);
  try {
    const result: any = await api.post(`/v1/admin/indices/${id}/fill-from-exchange`, {});
    const count = result?.data?.importedCount ?? 0;
    setFeedback({ type: 'success', message: `${count} symbolen toegevoegd vanuit beurs-listing` });
    queryClient.invalidateQueries({ queryKey: ['admin', 'indices'] });
    queryClient.invalidateQueries({ queryKey: ['exchanges'] });
  } catch (err: any) {
    const msg = err?.response?.data?.error ?? err?.message ?? 'Onbekende fout';
    setFeedback({ type: 'error', message: `Vul van beurs mislukt: ${msg}` });
  } finally {
    setFillingIndex(null);
  }
}
```

### 2d. Voeg feedback banner toe

Voeg toe direct onder de `<p>` beschrijving bovenaan (boven de Beursindexen sectie):

```tsx
{feedback && (
  <div className={`mb-4 px-4 py-3 rounded-lg text-sm flex items-center justify-between ${
    feedback.type === 'success'
      ? 'bg-green-500/10 border border-green-500/30 text-green-400'
      : 'bg-red-500/10 border border-red-500/30 text-red-400'
  }`}>
    <span>{feedback.message}</span>
    <button onClick={() => setFeedback(null)} className="ml-4 text-xs opacity-60 hover:opacity-100">×</button>
  </div>
)}
```

### 2e. Voeg "Vul van beurs" knop toe in de acties-kolom

In de index-tabel, voeg een extra knop toe naast de bestaande import en delete knoppen. Zoek de `<div className="flex items-center justify-center gap-2">` in de index-rij en voeg toe **voor** de delete-knop:

```tsx
<button
  onClick={() => handleFillFromExchange(idx.id)}
  disabled={fillingIndex === idx.id}
  className="p-1.5 rounded bg-amber-500/15 text-amber-400 hover:bg-amber-500/25 transition-colors disabled:opacity-50"
  title="Vul vanuit beurs-listing (fallback)"
>
  <Globe size={14} className={fillingIndex === idx.id ? 'animate-spin' : ''} />
</button>
```

De volgorde van knoppen per index-rij wordt dan:
1. ⬇ Import (blauw) — probeert EODHD fundamentals
2. 🌐 Vul van beurs (amber) — fallback, pakt alle symbolen van de exchange
3. 🗑 Verwijder (rood)

---

## Samenvatting

| Bestand | Actie |
|---------|-------|
| `src/.../Controllers/AdminController.cs` | **Gewijzigd** — nieuw `fill-from-exchange` endpoint |
| `frontend/src/pages/AdminExchangesPage.tsx` | **Gewijzigd** — feedback banner + fallback knop |

## Gebruik

1. Zorg dat de beurs (bijv. AS) eerst is ge\u00efmporteerd via "Actieve Beurzen" → Import
2. Ga naar de index (bijv. AEX 25) en klik op het **\ud83c\udf10 amber globe-icoontje** ("Vul van beurs")
3. Alle symbolen van exchange AS worden als AEX-leden toegevoegd
4. Op het Markets-scherm verschijnen ze nu onder de AEX 25 kolom

Noot: dit is een workaround — het voegt ALLE beurs-symbolen toe, niet alleen de echte index-componenten. Zodra je een hoger EODHD plan hebt, kun je de echte import-knop gebruiken.
