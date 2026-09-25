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

$repoRoot = Split-Path -Parent $PSScriptRoot

$solutions = Get-ChildItem -Path $repoRoot -Recurse -Include '*.sln', '*.slnx' |
    Where-Object { $_.FullName -notmatch '\\DemoProvider\\' }

if ($solutions.Count -eq 0) {
    Write-Host "No solutions found to build." -ForegroundColor Yellow
    exit 1
}

foreach ($solution in $solutions) {
    Write-Host ""
    Write-Host "Building $($solution.FullName) ($Configuration)..." -ForegroundColor Cyan
    
    if ($solution.Name -match 'Provider' -and $solution.Name -notmatch 'BaseProvider') {
        dotnet publish $solution.FullName -c $Configuration
    } else {
        dotnet build $solution.FullName -c $Configuration
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed: $($solution.FullName)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "All solutions built successfully." -ForegroundColor Green
