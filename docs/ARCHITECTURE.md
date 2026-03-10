# AxonStockAgent — Architectuur

## Overzicht

```
┌─────────────────────────────────────────────────────────┐
│                     NGINX (reverse proxy :80)                   │
│                    /api/* → API    /* → Frontend               │
└─────────────────┬───────────────────┬──────────────────┘
                 │                   │
      ┌─────────┴────────┐  ┌────┴────────────┐
      │  React Frontend  │  │  .NET Web API   │
      │  (Vite + TS)     │  │  (ASP.NET Core) │
      │  :3000           │  │  :5000          │
      └──────────────────┘  └────────┬────────┘
                                    │
                    ┌────────────┴────────────┐
                    │                           │
         ┌─────────┴───────┐  ┌────────┴─────────┐
         │  PostgreSQL    │  │  Redis           │
         │  :5432         │  │  :6379           │
         └────────────────┘  └─────────────────┘
                    │
         ┌─────────┴───────┐
         │  Worker Service │  (background screener)
         │  Finnhub + AI   │
         └────────────────┘
```

## Componenten

| Service | Tech | Doel |
|---------|------|------|
| **Frontend** | React + Vite + TypeScript + Tailwind | Dashboard, signalen, watchlist, portfolio |
| **API** | ASP.NET Core 8 + EF Core | REST endpoints, database CRUD, Swagger |
| **Worker** | .NET 8 Worker Service | Achtergrond scanning, AI enrichment, Telegram alerts |
| **Database** | PostgreSQL 16 | Signalen, watchlist, portfolio, dividenden, candle cache |
| **Cache** | Redis 7 | Rate limiting, API caching |
| **Proxy** | Nginx | Reverse proxy, routing frontend/api |

## Lokaal starten

```bash
# Kopieer environment variabelen
cp .env.example .env
# Vul je API keys in

# Development (met hot reload)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build

# Productie
docker compose up --build -d
```

Open http://localhost (nginx) of http://localhost:3000 (frontend direct)
API docs: http://localhost:5000/swagger

## Azure Deployment

Zie [AZURE_DEPLOY.md](./AZURE_DEPLOY.md) voor het volledige stappenplan.
