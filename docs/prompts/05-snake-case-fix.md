# Prompt 05 — Snake_case naming convention toevoegen aan DbContext

## Context
De Worker crasht met `column w.Symbol does not exist` — PostgreSQL hint: `Perhaps you meant to reference the column "w.symbol"`. De database gebruikt snake_case kolomnamen maar EF Core genereert PascalCase. De `UseSnakeCaseNamingConvention()` mist in beide Program.cs bestanden.

## Root cause
De `AppDbContext.OnModelCreating` mapt tabelnamen handmatig (`.ToTable("watchlist")`), maar **kolom**namen worden niet gemapped. Zonder `UseSnakeCaseNamingConvention()` genereert EF Core `"Symbol"` terwijl de kolom `symbol` heet.

## Stap 1: Voeg `UseSnakeCaseNamingConvention()` toe aan de Worker

Bestand: `src/AxonStockAgent.Worker/Program.cs`

Vervang:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Door:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());
```

## Stap 2: Voeg `UseSnakeCaseNamingConvention()` toe aan de API

Bestand: `src/AxonStockAgent.Api/Program.cs`

Vervang:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Door:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());
```

## Stap 3: Controleer of het NuGet package aanwezig is

Het package `EFCore.NamingConventions` moet in zowel het Api als Worker project zitten. Check de `.csproj` bestanden:

```bash
grep -r "NamingConventions" src/AxonStockAgent.Api/AxonStockAgent.Api.csproj
grep -r "NamingConventions" src/AxonStockAgent.Worker/AxonStockAgent.Worker.csproj
```

Als het in een van beide mist, voeg toe:

```bash
cd src/AxonStockAgent.Api && dotnet add package EFCore.NamingConventions
cd ../AxonStockAgent.Worker && dotnet add package EFCore.NamingConventions
```

## Stap 4: Verificatie

```bash
cd src && dotnet build
```

Moet 0 fouten geven.

## Na de prompt: herstart Docker

```bash
docker compose up -d --build worker api
```

Check daarna de worker logs:

```bash
docker compose logs -f worker
```

Je zou nu moeten zien: "Scan cycle gestart: X symbolen" in plaats van de column error.

## Commit
```
fix: add UseSnakeCaseNamingConvention to API and Worker DbContext

- Both Program.cs files now use .UseSnakeCaseNamingConvention()
- Fixes Worker crash: column w.Symbol does not exist
- EF Core now generates snake_case column names matching PostgreSQL schema
```
