# Script per generare e fidarsi del certificato HTTPS di sviluppo
# Eseguire UNA SOLA VOLTA per macchina, nella root del progetto
#
# Richiede .NET SDK installato e PowerShell in modalità amministratore (per --trust).
#
# Uso:
#   .\generate-dev-cert.ps1
#   oppure con password custom:
#   .\generate-dev-cert.ps1 -Password "MiaPassword"

param(
    [string]$Password = "MauwDev2024!"
)

$certPath = Join-Path $PSScriptRoot "certs\aspnetapp.pfx"

Write-Host "==> Generazione dev-cert HTTPS per localhost..." -ForegroundColor Cyan

# Genera (o rigenera) il dev-cert di ASP.NET Core e fidati nel sistema
dotnet dev-certs https --clean
dotnet dev-certs https --trust

# Esporta in formato PFX per il montaggio nel container Docker
dotnet dev-certs https -ep $certPath -p $Password

if ($LASTEXITCODE -eq 0) {
    Write-Host "==> Certificato esportato in: $certPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Imposta la password nei user-secrets o come variabile ambiente:" -ForegroundColor Yellow
    Write-Host "  dotnet user-secrets set `"Kestrel:Certificates:Default:Password`" `"$Password`"" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Ora puoi avviare con:  docker-compose up" -ForegroundColor Green
} else {
    Write-Host "ERRORE: generazione del certificato fallita." -ForegroundColor Red
    exit 1
}
