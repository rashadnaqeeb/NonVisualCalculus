# build.ps1 - Build NonVisualCalculus and deploy it into the game. Deploy itself is the
# NonVisualCalculus project's Debug post-build target (plugin + module into BepInEx\plugins,
# prism.dll next to disco.exe); this script locates the game, checks BepInEx is set up,
# and runs the build. Close the game first, or the dll copy is skipped (file locked)
# and you'll run a stale build.

param(
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-Help]"
    Write-Host "  Builds the solution and deploys the mod into the game folder."
    Write-Host "  Run setup-bepinex.ps1 once first, then launch the game once through Steam."
    exit 0
}

$ErrorActionPreference = "Stop"

# --- Locate the game install (same resolution as setup-bepinex.ps1) ---
$Game = $env:DISCO_ELYSIUM_DIR
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\Disco Elysium"
        if (Test-Path "$candidate\disco.exe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Disco Elysium" }
}
if (-not (Test-Path "$Game\disco.exe")) {
    Write-Host "ERROR: Disco Elysium not found at: $Game" -ForegroundColor Red
    Write-Host "Set the DISCO_ELYSIUM_DIR environment variable to the game folder." -ForegroundColor Red
    exit 1
}
$env:DISCO_ELYSIUM_DIR = $Game

if (-not (Test-Path "$Game\BepInEx\core\BepInEx.Core.dll")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx." -ForegroundColor Red
    Write-Host "Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\interop")) {
    Write-Host "ERROR: $Game\BepInEx\interop does not exist, so the build has nothing to compile against." -ForegroundColor Red
    Write-Host "Launch the game once through Steam and wait for the main menu (the first" -ForegroundColor Red
    Write-Host "launch generates it and can take a few minutes), then quit and re-run this." -ForegroundColor Red
    exit 1
}

# --- Build (the Debug post-build target deploys) ---
Write-Host "Building NonVisualCalculus (game: $Game)..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\NonVisualCalculus.slnx" -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Done. Launch Disco Elysium through Steam and listen for the startup line." -ForegroundColor Cyan
