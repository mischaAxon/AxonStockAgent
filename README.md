# AxonStockAgent

AI-gedreven aandelenscreener met real-time signalen, ML-voorspellingen en Claude AI reasoning.

## Tech Stack

| Laag | Technologie |
|------|-------------|
| Frontend | React + Vite + TypeScript + Tailwind CSS |
| API | ASP.NET Core 8 + Entity Framework Core |
| Worker | .NET 8 Worker Service (achtergrond scanning) |
| Database | PostgreSQL 16 |
| Cache | Redis 7 |
| AI | ML.NET FastTree + Claude API + Finnhub Sentiment |
| Infra | Docker Compose + Nginx reverse proxy |
| Deploy | Azure Container Apps (productie) |

## Snel starten

```bash
# 1. Clone
git clone https://github.com/mischaAxon/AxonStockAgent.git
cd AxonStockAgent

# 2. Environment variabelen
cp .env.example .env
# Vul je API keys in (.env bestand)

# 3. Start alles
docker compose up --build

# Of met hot reload (development):
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

Open http://localhost (dashboard) of http://localhost:5000/swagger (API docs)

## Project Structuur

```
AxonStockAgent/
├── docker-compose.yml          # Orchestratie (prod)
├── docker-compose.dev.yml      # Dev overrides (hot reload)
├── nginx/                      # Reverse proxy configuratie
├── frontend/                   # React dashboard
│   ├── src/pages/              # Dashboard, Signals, Watchlist, Portfolio
│   ├── src/hooks/              # TanStack Query API hooks
│   └── src/services/           # API client
├── src/
│   ├── AxonStockAgent.Core/    # Gedeelde modellen + interfaces
│   ├── AxonStockAgent.Api/     # REST API + EF Core
│   └── AxonStockAgent.Worker/  # Achtergrond scanner
├── database/init.sql           # Database schema
├── skills/                     # Claude skills voor ontwikkeling
└── docs/                       # Architectuur + Azure deploy guide
```

## Documentatie

- [Architectuur](docs/ARCHITECTURE.md) — Componenten overzicht
- [Azure Deploy](docs/AZURE_DEPLOY.md) — Productie deployment stappenplan
- [AI Layer](docs/AI_LAYER_README.md) — ML.NET + Claude + Sentiment uitleg

## Licentie

Private — Axon Factory
