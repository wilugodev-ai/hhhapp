param([switch]$ApiOnly)
$ErrorActionPreference = 'Stop'
$rootPath = Split-Path $PSScriptRoot -Parent
Set-Location $rootPath
$localSdk = Join-Path $rootPath '.tools\dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
if (Test-Path -LiteralPath $localSdk) {
    $env:DOTNET_ROOT = Split-Path $localSdk -Parent
    $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    $env:NUGET_PACKAGES = Join-Path $rootPath '.tools\nuget'
}
if ($ApiOnly) {
    & $dotnetCommand run --project src/Hhh.Api
} else {
    Write-Host 'Run scripts/dev.ps1 -ApiOnly in one terminal.'
    Write-Host 'Starting Angular at http://127.0.0.1:4300 in this terminal.'
    Set-Location (Join-Path $rootPath 'apps\web')
    npm start
}
