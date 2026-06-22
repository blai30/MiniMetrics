#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the framework-dependent Velopack release locally, matching the default channel produced by
    .github/workflows/release.yml.

.DESCRIPTION
    Mirrors the framework-dependent ("default" channel) half of the release pipeline:
      1. dotnet restore
      2. test gate (dotnet test, skippable with -SkipTests)
      3. dotnet publish (framework-dependent, no single-file) -> publish/fd-folder
      4. vpk pack on the default channel (--noPortable) -> velopack/default
      5. rename the bootstrapper to MiniMetrics-Setup.exe

    Unlike the workflow, this produces only the standalone Setup.exe: the Portable.zip is skipped and the
    self-contained channel is not built.

    The vpk CLI is pinned to the same Velopack version referenced by MiniMetrics.csproj so the packer and
    the in-app update runtime stay in lockstep.

.PARAMETER Version
    Release version (MAJOR.MINOR.PATCH, no leading v). Defaults to 0.0.0 to match the dev convention, where
    a 0.0.0 build reads as an unreleased dev build rather than a misleading 1.0.0.

.PARAMETER SkipTests
    Skip the dotnet test gate. The workflow always runs it; locally it is often noise during packaging
    iteration.

.EXAMPLE
    ./scripts/build-velopack.ps1
    Builds version 0.0.0 with the test gate.

.EXAMPLE
    ./scripts/build-velopack.ps1 -Version 1.3.0 -SkipTests
    Builds version 1.3.0 and skips the test gate.
#>
[CmdletBinding()]
param(
    [string]$Version = "0.0.0",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Version -notmatch "^[0-9]+\.[0-9]+\.[0-9]+$") {
    throw "Invalid version '$Version'. Expected MAJOR.MINOR.PATCH (e.g. 1.2.0)."
}

# Pin to the Velopack library version referenced by MiniMetrics.csproj so a newer vpk does not emit
# packages the pinned in-app updater cannot apply.
$vpkVersion = "1.2.0"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Host "Building MiniMetrics framework-dependent Velopack release $Version" -ForegroundColor Cyan

    Write-Host "`n[1/5] Restoring" -ForegroundColor Cyan
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    if ($SkipTests) {
        Write-Host "`n[2/5] Test gate skipped (-SkipTests)" -ForegroundColor Yellow
    } else {
        Write-Host "`n[2/5] Test gate" -ForegroundColor Cyan
        dotnet test --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet test failed." }
    }

    Write-Host "`n[3/5] Publishing framework-dependent folder" -ForegroundColor Cyan
    dotnet publish MiniMetrics.csproj -c Release -r win-x64 `
        --self-contained false `
        -p:PublishSingleFile=false `
        -p:Version=$Version `
        -o publish/fd-folder
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    Write-Host "`n[4/5] Ensuring vpk CLI $vpkVersion" -ForegroundColor Cyan
    $installed = dotnet tool list -g | Select-String -Pattern "^vpk\s+(\S+)"
    $installedVersion = if ($installed) { $installed.Matches[0].Groups[1].Value } else { $null }
    if ($installedVersion -ne $vpkVersion) {
        if ($installedVersion) {
            Write-Host "  Found vpk $installedVersion, switching to $vpkVersion"
            dotnet tool uninstall -g vpk
            if ($LASTEXITCODE -ne 0) { throw "dotnet tool uninstall vpk failed." }
        }
        dotnet tool install -g vpk --version $vpkVersion
        if ($LASTEXITCODE -ne 0) { throw "dotnet tool install vpk failed." }
    } else {
        Write-Host "  vpk $vpkVersion already installed"
    }

    Write-Host "`n[5/5] Packing Velopack default channel" -ForegroundColor Cyan
    # No --channel is passed, so vpk uses its built-in default channel (win): the same feed file names
    # (releases.win.json, the nupkg, RELEASES-win) the in-app updater fetches. No --framework is declared,
    # so the installer never downloads the .NET runtime; a missing runtime sends the user to the .NET
    # download on first launch via the apphost.
    # --noPortable skips the Portable.zip the release workflow also ships; this local build only wants the
    # standalone Setup.exe.
    vpk pack `
        --packId MiniMetrics `
        --packVersion $Version `
        --packDir publish/fd-folder `
        --mainExe MiniMetrics.exe `
        --packTitle MiniMetrics `
        --packAuthors "Brian Lai" `
        --icon Assets/minimetrics.ico `
        --noPortable `
        --outputDir velopack/default
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed." }

    # Give the bootstrapper the friendly name the release uses. The .nupkg and releases.*.json feed files
    # are left untouched because the in-app updater fetches them by their exact names.
    function Set-AssetName($dir, $filter, $target) {
        $file = Get-ChildItem $dir -Filter $filter | Select-Object -First 1
        if ($file -and $file.Name -ne $target) {
            Rename-Item -Path $file.FullName -NewName $target
        }
    }
    Set-AssetName velopack/default "*Setup.exe" "MiniMetrics-Setup.exe"

    Write-Host "`nDone. Artifacts in velopack/default:" -ForegroundColor Green
    Get-ChildItem velopack/default -File | ForEach-Object { Write-Host "  $($_.Name)" }
}
finally {
    Pop-Location
}
