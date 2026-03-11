FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopieer project files voor restore
COPY AxonStockAgent.Core/AxonStockAgent.Core.csproj AxonStockAgent.Core/
COPY AxonStockAgent.Api/AxonStockAgent.Api.csproj AxonStockAgent.Api/
COPY AxonStockAgent.Worker/AxonStockAgent.Worker.csproj AxonStockAgent.Worker/
RUN dotnet restore AxonStockAgent.Worker/AxonStockAgent.Worker.csproj

# Kopieer alle source en build
COPY . .
RUN dotnet publish AxonStockAgent.Worker/AxonStockAgent.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "AxonStockAgent.Worker.dll"]
