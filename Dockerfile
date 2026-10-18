# API .NET (il frontend Angular ha un Dockerfile separato in src/Mauw.Erp.Web)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-builder
WORKDIR /src

# Restore separato dal sorgente per sfruttare la cache dei layer
COPY ["Directory.Packages.props", "./"]
COPY ["src/Mauw.Erp.Api/Mauw.Erp.Api.csproj", "src/Mauw.Erp.Api/"]
COPY ["src/Mauw.Erp.Application/Mauw.Erp.Application.csproj", "src/Mauw.Erp.Application/"]
COPY ["src/Mauw.Erp.Contracts/Mauw.Erp.Contracts.csproj", "src/Mauw.Erp.Contracts/"]
COPY ["src/Mauw.Erp.Domain/Mauw.Erp.Domain.csproj", "src/Mauw.Erp.Domain/"]
COPY ["src/Mauw.Erp.Infrastructure/Mauw.Erp.Infrastructure.csproj", "src/Mauw.Erp.Infrastructure/"]
RUN dotnet restore "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj"

COPY ["src/", "src/"]

# Publish stage
FROM dotnet-builder AS publish
RUN dotnet publish "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Mauw.Erp.Api.dll"]
