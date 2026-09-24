<#
.SYNOPSIS
    Ensures the .NET runtimes WindowsSearch needs to run are installed.

.DESCRIPTION
    Hub.exe needs the .NET Windows Desktop runtime (WPF/WinForms UI).
    Providers (e.g. JetBrainsProvider.exe, DemoProvider.exe) are framework-dependent
    and need the ASP.NET Core runtime to host their gRPC transport, even when a
    different transport (NamedPipe/Http) is actually selected at runtime, because
    the .NET host resolves shared frameworks before any app code runs.

    This script checks for the required runtimes (x64) and installs any that are
    missing via winget. Run it once per machine before running a published build.

.PARAMETER Architecture
    The runtime architecture to check/install. Defaults to x64, matching the
    RuntimeIdentifier the providers are published with.
#>

[CmdletBinding()]
param(
    [string]$Architecture = 'x64'
)

$ErrorActionPreference = 'Stop'

$requiredRuntimes = @(
    [pscustomobject]@{ Name = 'Microsoft.WindowsDesktop.App'; WingetId = 'Microsoft.DotNet.DesktopRuntime.10'; Reason = "Hub's WPF/WinForms UI" }
    [pscustomobject]@{ Name = 'Microsoft.AspNetCore.App';      WingetId = 'Microsoft.DotNet.AspNetCore.10';    Reason = "providers' gRPC transport (BaseProvider)" }
)

function Test-RuntimeInstalled {
    param([string]$Name, [string]$Architecture)

    $programFiles = if ($Architecture -eq 'x86') { ${env:ProgramFiles(x86)} } else { $env:ProgramFiles }
    $sharedDir = Join-Path $programFiles "dotnet\shared\$Name"

    if (-not (Test-Path $sharedDir)) {
        return $false
    }

    return (Get-ChildItem $sharedDir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '10.*' }).Count -gt 0
}

$missing = $requiredRuntimes | Where-Object { -not (Test-RuntimeInstalled -Name $_.Name -Architecture $Architecture) }

if ($missing.Count -eq 0) {
    Write-Host "All required .NET runtimes ($Architecture) are already installed." -ForegroundColor Green
    exit 0
}

Write-Host "Missing .NET runtimes ($Architecture):" -ForegroundColor Yellow
foreach ($runtime in $missing) {
    Write-Host "  - $($runtime.Name) (needed for $($runtime.Reason))"
}

$winget = Get-Command winget -ErrorAction SilentlyContinue
if (-not $winget) {
    Write-Host ""
    Write-Host "winget was not found. Install the runtimes above manually from:" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0"
    exit 1
}

foreach ($runtime in $missing) {
    Write-Host ""
    Write-Host "Installing $($runtime.WingetId)..." -ForegroundColor Cyan
    winget install --id $runtime.WingetId --architecture $Architecture --accept-source-agreements --accept-package-agreements -e
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to install $($runtime.WingetId). Install it manually from https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "All required runtimes installed." -ForegroundColor Green
