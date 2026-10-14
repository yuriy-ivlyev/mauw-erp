# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

COPY ["Mauw.Erp.sln", "."]
COPY ["src/", "src/"]

RUN dotnet restore "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj"
RUN dotnet build "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj" -c Release -o /app/build

# Publish stage
FROM builder AS publish
RUN dotnet publish "src/Mauw.Erp.Api/Mauw.Erp.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Mauw.Erp.Api.dll"]
