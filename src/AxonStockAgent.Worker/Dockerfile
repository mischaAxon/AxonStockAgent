FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY AxonStockAgent.Core/AxonStockAgent.Core.csproj AxonStockAgent.Core/
COPY AxonStockAgent.Worker/AxonStockAgent.Worker.csproj AxonStockAgent.Worker/
RUN dotnet restore AxonStockAgent.Worker/AxonStockAgent.Worker.csproj

COPY . .
RUN dotnet publish AxonStockAgent.Worker/AxonStockAgent.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS production
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
COPY --from=build /app/publish .

USER appuser
ENTRYPOINT ["dotnet", "AxonStockAgent.Worker.dll"]
