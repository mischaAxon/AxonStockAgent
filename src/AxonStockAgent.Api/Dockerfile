# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AxonStockAgent.Core/AxonStockAgent.Core.csproj AxonStockAgent.Core/
COPY AxonStockAgent.Api/AxonStockAgent.Api.csproj AxonStockAgent.Api/
RUN dotnet restore AxonStockAgent.Api/AxonStockAgent.Api.csproj

COPY . .
RUN dotnet publish AxonStockAgent.Api/AxonStockAgent.Api.csproj -c Release -o /app/publish

# ---- Production stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS production
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
COPY --from=build /app/publish .

USER appuser
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s \
  CMD wget --no-verbose --tries=1 -O /dev/null http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "AxonStockAgent.Api.dll"]
