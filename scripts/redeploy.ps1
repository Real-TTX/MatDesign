#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Rebuilds the MatDesign container and redeploys the dev stack (the project's live-reload / test workflow).
.DESCRIPTION
    Always rebuilds the image from the current source and recreates the container. The site keeps no state, so there is no volume to keep or to wipe.
    The version of a local build is "local-<builddate>".
.PARAMETER Port
    Host port of the web interface (default 8060).
.EXAMPLE
    ./scripts/redeploy.ps1
.EXAMPLE
    ./scripts/redeploy.ps1 -Port 8160
#>
param(
    [int]$Port = 8060
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $env:MATDESIGN_CHANNEL = 'local'
    $env:MATDESIGN_BUILD_DATE = (Get-Date).ToUniversalTime().ToString('yyyyMMdd')
    $env:MATDESIGN_PORT = "$Port"

    $compose = @('compose', '-f', 'docker-compose.dev.yml')

    # Windows PowerShell 5.1 turns the progress lines docker writes to stderr into errors; judge by the exit code instead.
    $ErrorActionPreference = 'Continue'

    docker @compose up -d --build --remove-orphans
    if ($LASTEXITCODE -ne 0) { throw 'docker compose failed.' }

    Write-Host ''
    Write-Host "MatDesign local-$($env:MATDESIGN_BUILD_DATE) is running on http://localhost:$Port" -ForegroundColor Green
}
finally {
    Pop-Location
}
