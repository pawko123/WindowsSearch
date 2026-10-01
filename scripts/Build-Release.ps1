<#
.SYNOPSIS
    Builds all WindowsSearch solutions in Release configuration.

.DESCRIPTION
    Discovers every .sln/.slnx file in the repo (Hub.sln, Shared.slnx,
    JetBrainsProvider.slnx, etc.) and runs `dotnet build -c Release` against
    each. DemoProvider has no solution of its own and is a sample provider,
    not part of the shipped release, so it is skipped.

.PARAMETER Configuration
    Build configuration to use. Defaults to Release.
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

Write-Host "Stopping running Hub and Provider processes..." -ForegroundColor Yellow
Get-Process -Name "Hub", "*Provider" -ErrorAction SilentlyContinue | Stop-Process -Force

$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Building Shared..." -ForegroundColor Cyan
dotnet build $repoRoot/Shared/Shared.slnx -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nBuilding Hub..." -ForegroundColor Cyan
dotnet build $repoRoot/Hub/Hub.slnx -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nBuilding Providers..." -ForegroundColor Cyan
dotnet build $repoRoot/Providers/Providers.slnx -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nPublishing Providers..." -ForegroundColor Cyan
$providerDirs = Get-ChildItem -Path "$repoRoot/Providers" -Directory
foreach ($dir in $providerDirs) {
    $name = $dir.Name
    if ($name -in @('DemoProvider', 'BaseProvider', 'WebBaseProvider')) { continue }
    
    $csprojPath = "$($dir.FullName)/$name/$name.csproj"
    if (Test-Path $csprojPath) {
        Write-Host "Publishing $name..."
        dotnet publish $csprojPath -c $Configuration --no-build
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed: $name" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }
}

Write-Host ""
Write-Host "All solutions built successfully." -ForegroundColor Green
