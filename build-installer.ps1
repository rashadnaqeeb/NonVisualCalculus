# Build the Windows installer, releases\NonVisualCalculusInstaller.exe: the Loadstone mod manager
# branded as a single-mod installer by installer\loadstone-profile.json, which pins it to this
# repo's loadstone-packages\ registry. Loadstone is a checkout beside this repo (..\loadstone),
# or wherever LOADSTONE_DIR points. Its build script needs Visual Studio's C++ workload, CMake and
# Rust; the first run builds wxWidgets once into %LOCALAPPDATA%\loadstone.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $scriptDir "releases"
$profilePath = Join-Path $scriptDir "installer\loadstone-profile.json"
$loadstoneDir = $env:LOADSTONE_DIR
if ([string]::IsNullOrWhiteSpace($loadstoneDir)) {
    $loadstoneDir = Join-Path (Split-Path -Parent $scriptDir) "loadstone"
}
$buildScript = Join-Path $loadstoneDir "scripts\build_windows_app.ps1"

if (-not (Test-Path $buildScript)) {
    throw "Loadstone checkout not found: $buildScript (clone github.com/bradjrenshaw/loadstone beside this repo or set LOADSTONE_DIR)"
}

New-Item -ItemType Directory -Force $releaseDir | Out-Null
& $buildScript -ProfilePath $profilePath -Name "NonVisualCalculusInstaller" -Out $releaseDir
Write-Host "Installer: $(Join-Path $releaseDir 'NonVisualCalculusInstaller.exe')"
