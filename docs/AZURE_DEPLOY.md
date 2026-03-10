# AxonStockAgent — Azure Deployment Guide

## Aanbevolen: Azure Container Apps

Azure Container Apps is de beste keuze voor dit project:
- **Goedkoper** dan AKS (geen cluster management kosten)
- **Schaalt naar nul** wanneer niet in gebruik (betaal alleen wat je gebruikt)
- **Docker Compose compatibel** via `az containerapp compose create`
- **Built-in Dapr** voor service-to-service communicatie
- **Managed identity** voor veilige verbindingen

### Geschatte kosten (West Europe)

| Component | Schatting/maand |
|-----------|----------------|
| Container Apps (API + Worker) | ~€5-15 (laag verkeer, scale-to-zero) |
| Azure Database for PostgreSQL Flex | ~€15-25 (Burstable B1ms) |
| Azure Cache for Redis | ~€15 (Basic C0) |
| Container Registry | ~€5 (Basic) |
| **Totaal** | **~€40-60/maand** |

### Alternatief: Goedkoper met een VPS

Als je kosten wilt minimaliseren:
- **Hetzner Cloud CX22** (~€4/mnd): 2 vCPU, 4GB RAM — draai alles met Docker Compose
- **Oracle Cloud Free Tier**: ARM VM met 4 OCPU + 24GB RAM (gratis!)
- Zelf PostgreSQL en Redis draaien in Docker

---

## Stap-voor-stap: Azure Container Apps

### 1. Prerequisites

```bash
# Azure CLI installeren
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Login
az login

# Extensions installeren
az extension add --name containerapp --upgrade
```

### 2. Resource Group + Container Registry

```bash
# Variabelen
RG="axon-rg"
LOCATION="westeurope"
ACR="axonstockagent"

# Resource group
az group create --name $RG --location $LOCATION

# Container Registry
az acr create --name $ACR --resource-group $RG --sku Basic --admin-enabled true
az acr login --name $ACR
```

### 3. Images bouwen en pushen

```bash
# Build en push alle images
docker compose build

# Tag en push naar ACR
for SERVICE in api worker frontend; do
  docker tag axonstockagent-$SERVICE $ACR.azurecr.io/$SERVICE:latest
  docker push $ACR.azurecr.io/$SERVICE:latest
done
```

### 4. Database + Redis aanmaken

```bash
# PostgreSQL Flexible Server
az postgres flexible-server create \
  --resource-group $RG \
  --name axon-db \
  --location $LOCATION \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --admin-user axonadmin \
  --admin-password <STERK_WACHTWOORD> \
  --database-name axonstockagent

# Redis Cache
az redis create \
  --resource-group $RG \
  --name axon-cache \
  --location $LOCATION \
  --sku Basic \
  --vm-size C0
```

### 5. Container Apps Environment

```bash
# Environment aanmaken
az containerapp env create \
  --name axon-env \
  --resource-group $RG \
  --location $LOCATION

# API container app
az containerapp create \
  --name axon-api \
  --resource-group $RG \
  --environment axon-env \
  --image $ACR.azurecr.io/api:latest \
  --registry-server $ACR.azurecr.io \
  --target-port 5000 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 2 \
  --env-vars \
    "ASPNETCORE_URLS=http://+:5000" \
    "ConnectionStrings__DefaultConnection=<POSTGRES_CONN_STRING>" \
    "Screener__FinnhubApiKey=<KEY>" \
    "Screener__ClaudeApiKey=<KEY>"

# Worker container app (geen ingress nodig)
az containerapp create \
  --name axon-worker \
  --resource-group $RG \
  --environment axon-env \
  --image $ACR.azurecr.io/worker:latest \
  --registry-server $ACR.azurecr.io \
  --min-replicas 1 \
  --max-replicas 1 \
  --env-vars \
    "ConnectionStrings__DefaultConnection=<POSTGRES_CONN_STRING>"

# Frontend container app
az containerapp create \
  --name axon-frontend \
  --resource-group $RG \
  --environment axon-env \
  --image $ACR.azurecr.io/frontend:latest \
  --registry-server $ACR.azurecr.io \
  --target-port 3000 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 2
```

### 6. Custom domain (optioneel)

```bash
az containerapp hostname add \
  --name axon-frontend \
  --resource-group $RG \
  --hostname app.axonfactory.ai
```

---

## CI/CD met GitHub Actions

Maak `.github/workflows/deploy.yml` aan:

```yaml
name: Deploy to Azure
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - run: az acr login --name axonstockagent
      - run: |
          docker compose build
          for SERVICE in api worker frontend; do
            docker tag axonstockagent-$SERVICE axonstockagent.azurecr.io/$SERVICE:${{ github.sha }}
            docker push axonstockagent.azurecr.io/$SERVICE:${{ github.sha }}
          done
      - run: |
          for APP in axon-api axon-worker axon-frontend; do
            az containerapp update --name $APP --resource-group axon-rg --image axonstockagent.azurecr.io/${APP#axon-}:${{ github.sha }}
          done
```
