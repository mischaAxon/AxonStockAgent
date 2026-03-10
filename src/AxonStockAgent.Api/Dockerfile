# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY AxonStockAgent.Core/AxonStockAgent.Core.csproj AxonStockAgent.Core/
COPY AxonStockAgent.Api/AxonStockAgent.Api.csproj AxonStockAgent.Api/
RUN dotnet restore AxonStockAgent.Api/AxonStockAgent.Api.csproj

COPY . .
RUN dotnet publish AxonStockAgent.Api/AxonStockAgent.Api.csproj -c Release -o /app/publish --no-restore

# ---- Production stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS production
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
COPY --from=build /app/publish .

USER appuser
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
  CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "AxonStockAgent.Api.dll"]
