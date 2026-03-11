# Prompt 04 — Markturen check configureerbaar maken

## Context
De ScreenerWorker scant alleen tijdens markturen (8:00-21:00 UTC, ma-vr). Voor testing willen we dit kunnen overriden via een config setting.

## Stap 1: Voeg `IgnoreMarketHours` toe aan ScreenerConfig

Bestand: `src/AxonStockAgent.Core/Models/ScreenerConfig.cs` (of waar ScreenerConfig gedefinieerd is)

Voeg dit property toe:

```csharp
public bool IgnoreMarketHours { get; set; } = false;
```

## Stap 2: Pas de `IsMarketHours` check aan in ScreenerWorker

Bestand: `src/AxonStockAgent.Worker/ScreenerWorker.cs`

Vervang de huidige `IsMarketHours` methode:

```csharp
private static bool IsMarketHours()
{
    var now = DateTime.UtcNow;
    if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
    return now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(21);
}
```

Door:

```csharp
private bool IsMarketHours()
{
    if (_config.IgnoreMarketHours) return true;
    var now = DateTime.UtcNow;
    if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
    return now.TimeOfDay >= TimeSpan.FromHours(8) && now.TimeOfDay <= TimeSpan.FromHours(21);
}
```

Let op: de methode is niet meer `static` omdat hij `_config` leest.

## Stap 3: Voeg de env var toe aan docker-compose.yml

Bestand: `docker-compose.yml`

Bij de `worker` service, voeg deze environment variable toe:

```yaml
- Screener__IgnoreMarketHours=${IGNORE_MARKET_HOURS:-false}
```

## Stap 4: Verificatie

```bash
cd src && dotnet build
```

## Gebruik

Om buiten markturen te testen:

```bash
# In .env:
IGNORE_MARKET_HOURS=true

# Of direct:
IGNORE_MARKET_HOURS=true docker compose up -d worker
```

## Commit
```
feat: make market hours check configurable via IGNORE_MARKET_HOURS

- Added IgnoreMarketHours property to ScreenerConfig
- Worker skips market hours check when set to true
- Useful for testing outside trading hours
```
