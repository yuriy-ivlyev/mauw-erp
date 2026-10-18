#requires -Version 5.1
<#
.SYNOPSIS
    Builda solo i servizi modificati (api / web / proxy) e avvia docker compose.

.PARAMETER Target
    auto  (default) rileva le modifiche rispetto all'ultima build riuscita
    all   ricostruisce api, web e proxy
    api | web | proxy   ricostruisce solo il servizio indicato
    none  nessuna build, solo 'up'
    down  ferma e rimuove i container (i volumi restano)

.PARAMETER NoCache
    Build senza cache Docker per i servizi selezionati.
#>
param(
    [ValidateSet('auto', 'all', 'api', 'web', 'proxy', 'none', 'down')]
    [string]$Target = 'auto',
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stateFile = Join-Path $root '.vscode\.dev-build-state.json'
$images = @{ api = 'mauw-erp-api:local'; web = 'mauw-erp-web:local'; proxy = 'mauw-erp-proxy:local' }

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker $($args -join ' ') ha restituito $LASTEXITCODE" }
}

if ($Target -eq 'down') {
    Invoke-Docker compose down --remove-orphans
    return
}

# Hash del contenuto sorgente che finisce nell'immagine di ciascun servizio
function Get-SourceHash([string[]]$Paths, [string[]]$ExcludeRegex) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $files = foreach ($p in $Paths) {
        $full = Join-Path $root $p
        if (Test-Path $full -PathType Container) { Get-ChildItem $full -Recurse -File -Force }
        elseif (Test-Path $full) { Get-Item $full -Force }
    }
    $files = $files | Where-Object {
        $rel = $_.FullName.Substring($root.Length).TrimStart('\')
        -not ($ExcludeRegex | Where-Object { $rel -match $_ })
    } | Sort-Object FullName
    foreach ($f in $files) {
        $line = $f.FullName.Substring($root.Length) + ':' + (Get-FileHash $f.FullName -Algorithm SHA256).Hash
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($line)
        [void]$sha.TransformBlock($bytes, 0, $bytes.Length, $null, 0)
    }
    [void]$sha.TransformFinalBlock(@(), 0, 0)
    [BitConverter]::ToString($sha.Hash).Replace('-', '')
}

$buildDirs = '(^|\\)(bin|obj|node_modules|dist|\.angular)(\\|$)'
$hashes = [ordered]@{
    api   = Get-SourceHash @('src', 'Directory.Packages.props', 'Dockerfile', '.dockerignore') @($buildDirs, '^src\\Mauw\.Erp\.Web(\\|$)')
    web   = Get-SourceHash @('src\Mauw.Erp.Web') @($buildDirs)
    proxy = Get-SourceHash @('proxy') @()
}

$previous = @{}
if (Test-Path $stateFile) {
    (Get-Content $stateFile -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $previous[$_.Name] = $_.Value }
}

function Test-ImageExists([string]$Image) {
    & docker image inspect $Image *> $null
    $LASTEXITCODE -eq 0
}

$toBuild = switch ($Target) {
    'auto' { $hashes.Keys | Where-Object { $previous[$_] -ne $hashes[$_] -or -not (Test-ImageExists $images[$_]) } }
    'all'  { $hashes.Keys }
    'none' { @() }
    default { @($Target) }
}
$toBuild = @($toBuild)

if ($toBuild.Count -gt 0) {
    Write-Host "==> Build: $($toBuild -join ', ')" -ForegroundColor Cyan
    $buildArgs = @('compose', 'build')
    if ($NoCache) { $buildArgs += '--no-cache' }
    Invoke-Docker @buildArgs @toBuild
    foreach ($svc in $toBuild) { $previous[$svc] = $hashes[$svc] }
    New-Item -ItemType Directory -Force (Split-Path $stateFile) | Out-Null
    $previous | ConvertTo-Json | Set-Content $stateFile -Encoding UTF8
} else {
    Write-Host '==> Nessuna modifica: build saltata.' -ForegroundColor Green
}

Write-Host '==> docker compose up' -ForegroundColor Cyan
Invoke-Docker compose up -d --remove-orphans

$port = if ($env:HTTPS_PORT) { $env:HTTPS_PORT } else { '5019' }
Write-Host "==> Pronto: https://localhost:$port" -ForegroundColor Green
