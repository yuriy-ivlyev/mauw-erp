$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$secretStorePath = Join-Path $env:APPDATA 'Microsoft\UserSecrets\mauw-erp-api-secrets\secrets.json'
if (-not (Test-Path -LiteralPath $secretStorePath)) {
    throw 'API User Secrets non trovati. Verificare UserSecretsId e configurazione locale.'
}

$settings = Get-Content -LiteralPath $secretStorePath -Raw | ConvertFrom-Json
$accessKeyProperty = $settings.PSObject.Properties['AWS:AccessKeyId']
$secretKeyProperty = $settings.PSObject.Properties['AWS:SecretAccessKey']
if ($null -eq $accessKeyProperty -or [string]::IsNullOrWhiteSpace([string]$accessKeyProperty.Value) -or
    $null -eq $secretKeyProperty -or [string]::IsNullOrWhiteSpace([string]$secretKeyProperty.Value)) {
    throw 'AWS:AccessKeyId e AWS:SecretAccessKey devono essere presenti nei User Secrets.'
}

$secretDirectory = Join-Path $repositoryRoot '.secrets'
New-Item -ItemType Directory -Path $secretDirectory -Force | Out-Null
[System.IO.File]::WriteAllText(
    (Join-Path $secretDirectory 'aws_access_key_id'),
    [string]$accessKeyProperty.Value)
[System.IO.File]::WriteAllText(
    (Join-Path $secretDirectory 'aws_secret_access_key'),
    [string]$secretKeyProperty.Value)

Remove-Variable settings, accessKeyProperty, secretKeyProperty -ErrorAction SilentlyContinue
Write-Output 'AWS secret files created locally; their values were not displayed.'
