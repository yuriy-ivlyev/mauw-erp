#requires -Version 5.1
<#
.SYNOPSIS
    Builda solo i servizi modificati (api / web / proxy) e avvia docker compose.
    Se sono configurate credenziali AWS locali, le monta come Compose secrets.

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

$composeFiles = @('-f', 'docker-compose.yml')
$secretStorePath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\mauw-erp-api-secrets\secrets.json'
if (Test-Path -LiteralPath $secretStorePath) {
    $settings = Get-Content -LiteralPath $secretStorePath -Raw | ConvertFrom-Json
    $regionProperty = $settings.PSObject.Properties['AWS:Region']
    if ($null -ne $regionProperty -and
        -not [string]::IsNullOrWhiteSpace([string]$regionProperty.Value)) {
        $env:AWS_REGION = [string]$regionProperty.Value
    }

    $textractRegionProperty = $settings.PSObject.Properties['AWS:TextractRegion']
    if ($null -ne $textractRegionProperty -and
        -not [string]::IsNullOrWhiteSpace([string]$textractRegionProperty.Value)) {
        $env:AWS_TEXTRACT_REGION = [string]$textractRegionProperty.Value
    } elseif ([string]::IsNullOrWhiteSpace($env:AWS_TEXTRACT_REGION)) {
        $env:AWS_TEXTRACT_REGION = 'eu-central-1'
    }

    $accessKeyProperty = $settings.PSObject.Properties['AWS:AccessKeyId']
    $secretKeyProperty = $settings.PSObject.Properties['AWS:SecretAccessKey']
    $hasAccessKey = $null -ne $accessKeyProperty -and
        -not [string]::IsNullOrWhiteSpace([string]$accessKeyProperty.Value)
    $hasSecretKey = $null -ne $secretKeyProperty -and
        -not [string]::IsNullOrWhiteSpace([string]$secretKeyProperty.Value)

    if ($hasAccessKey -xor $hasSecretKey) {
        throw 'AWS:AccessKeyId e AWS:SecretAccessKey devono essere configurati insieme.'
    }

    if ($hasAccessKey) {
        & (Join-Path $PSScriptRoot 'export-aws-textract-secrets.ps1')
        $composeFiles += @('-f', 'docker-compose.aws-secrets.yml')
    }

    Remove-Variable settings, regionProperty, textractRegionProperty, accessKeyProperty, secretKeyProperty -ErrorAction SilentlyContinue
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
    $buildArgs = @('compose') + $composeFiles + @('build')
    if ($NoCache) { $buildArgs += '--no-cache' }
    $buildArgs += $toBuild
    Invoke-Docker @buildArgs
    foreach ($svc in $toBuild) { $previous[$svc] = $hashes[$svc] }
    New-Item -ItemType Directory -Force (Split-Path $stateFile) | Out-Null
    $previous | ConvertTo-Json | Set-Content $stateFile -Encoding UTF8
} else {
    Write-Host '==> Nessuna modifica: build saltata.' -ForegroundColor Green
}

Write-Host '==> docker compose up' -ForegroundColor Cyan
$upArgs = @('compose') + $composeFiles + @('up', '-d', '--remove-orphans')
Invoke-Docker @upArgs

$port = if ($env:HTTPS_PORT) { $env:HTTPS_PORT } else { '5019' }
Write-Host "==> Pronto: https://localhost:$port" -ForegroundColor Green
