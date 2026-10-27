param([switch]$ApiOnly, [switch]$Demo)
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
    if ($Demo) {
        $env:Demo__Enabled = 'true'
        $env:ConnectionStrings__Hhh = 'Server=.\SQLEXPRESS01;Database=HhhDevelopment;Trusted_Connection=True;TrustServerCertificate=True'
        Write-Host 'Starting the optional reference demo database.'
    } else {
        $env:Demo__Enabled = 'false'
        if ($env:ConnectionStrings__Hhh -eq 'Server=.\SQLEXPRESS01;Database=HhhDevelopment;Trusted_Connection=True;TrustServerCertificate=True') {
            Remove-Item -LiteralPath 'Env:\ConnectionStrings__Hhh'
        }
        if (-not $env:Bootstrap__SetupKey) {
            $privateDirectory = Join-Path $rootPath '.artifacts'
            New-Item -ItemType Directory -Path $privateDirectory -Force | Out-Null
            $setupKeyPath = Join-Path $privateDirectory 'first-admin-setup.key'
            if (-not (Test-Path -LiteralPath $setupKeyPath)) {
                New-Item -ItemType File -Path $setupKeyPath | Out-Null
                if ($env:OS -eq 'Windows_NT') {
                    $keyAcl = Get-Acl -LiteralPath $setupKeyPath
                    $keyAcl.SetAccessRuleProtection($true, $false)
                    $keyOwner = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
                    $keyAcl.SetAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new($keyOwner, 'FullControl', 'Allow'))
                    Set-Acl -LiteralPath $setupKeyPath -AclObject $keyAcl
                }
                $setupRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
                try {
                    $setupBytes = New-Object byte[] 32
                    $setupRandom.GetBytes($setupBytes)
                    [System.IO.File]::WriteAllText($setupKeyPath, [Convert]::ToBase64String($setupBytes))
                } finally { $setupRandom.Dispose() }
            }
            $env:Bootstrap__SetupKey = (Get-Content -LiteralPath $setupKeyPath -Raw).Trim()
            if ($env:Bootstrap__SetupKey.Length -lt 32) { throw 'The private installation key is invalid.' }
            Write-Host "First-administrator installation key file: $setupKeyPath"
        }
        Write-Host 'First administrator: http://127.0.0.1:4300/setup'
    }
    & $dotnetCommand run --project src/Hhh.Api
} else {
    Write-Host 'Run scripts/dev.ps1 -ApiOnly in one terminal.'
    Write-Host 'Starting Angular at http://127.0.0.1:4300 in this terminal.'
    Set-Location (Join-Path $rootPath 'apps\web')
    npm start
}
