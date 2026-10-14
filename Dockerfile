# Build stage - Node.js
FROM node:20-alpine AS web-builder
WORKDIR /app/web

COPY src/Mauw.Erp.Api/web/package*.json ./
RUN npm ci --legacy-peer-deps

COPY src/Mauw.Erp.Api/web .
RUN npm run build

# Build stage - .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-builder
WORKDIR /src

COPY ["Mauw.Erp.sln", "."]
COPY ["src/", "src/"]

RUN dotnet restore "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj"
RUN dotnet build "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj" -c Release -o /app/build

# Publish stage
FROM dotnet-builder AS publish
RUN dotnet publish "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .
COPY --from=web-builder /app/web/dist/web/browser ./wwwroot

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Mauw.Erp.Api.dll"]

