# Build the distributable mod zip: the vendored BepInEx game-folder layout, the Release
# plugin output under BepInEx\plugins\WhirlingInWords, the audio assets and lang files
# beside the plugin, and prism.dll at the game-folder root. The zip root IS the game
# folder, so the installer (and a manual user) extracts it straight into the game dir.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$propsPath = Join-Path $scriptDir "Directory.Build.props"
$releaseDir = Join-Path $scriptDir "releases"
$stageDir = Join-Path $scriptDir "obj\release-stage"

[xml]$props = Get-Content $propsPath
$versionNode = $props.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Could not read Version from $propsPath"
}
$version = $versionNode.InnerText.Trim()

$bepinexZip = Join-Path $scriptDir "third_party\bepinex\BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip"
$prismDll = Join-Path $scriptDir "third_party\prism\prism.dll"
$hostOutDir = Join-Path $scriptDir "src\WhirlingInWords\bin\Release\net6.0"
$moduleDll = Join-Path $scriptDir "src\WhirlingInWords.Module\bin\Release\net6.0\WhirlingInWords.Module.dll"
$zipPath = Join-Path $releaseDir "WhirlingInWords-v$version.zip"

foreach ($required in @($bepinexZip, $prismDll)) {
    if (-not (Test-Path $required)) {
        throw "Required file not found: $required"
    }
}

Push-Location $scriptDir
try {
    dotnet build WhirlingInWords.slnx -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    $pluginDll = Join-Path $hostOutDir "WhirlingInWords.dll"
    foreach ($required in @($pluginDll, $moduleDll)) {
        if (-not (Test-Path $required)) {
            throw "Release build output not found: $required"
        }
    }

    if (Test-Path $stageDir) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force $stageDir | Out-Null
    New-Item -ItemType Directory -Force $releaseDir | Out-Null

    # The BepInEx zip root is already the game-folder layout (winhttp.dll,
    # doorstop_config.ini, BepInEx\, dotnet\).
    Expand-Archive -Path $bepinexZip -DestinationPath $stageDir -Force

    $pluginDir = Join-Path $stageDir "BepInEx\plugins\WhirlingInWords"
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    Copy-Item -Path (Join-Path $hostOutDir "*.dll") -Destination $pluginDir
    Copy-Item -LiteralPath $moduleDll -Destination $pluginDir

    foreach ($assetSet in @("cursor", "interactables", "walltones\1")) {
        $assetDir = Join-Path $pluginDir "assets\audio\$assetSet"
        New-Item -ItemType Directory -Force $assetDir | Out-Null
        Copy-Item -Path (Join-Path $scriptDir "assets\audio\$assetSet\*.wav") -Destination $assetDir
    }

    $langDir = Join-Path $pluginDir "lang"
    New-Item -ItemType Directory -Force $langDir | Out-Null
    Copy-Item -Path (Join-Path $scriptDir "lang\*.txt") -Destination $langDir

    Copy-Item -LiteralPath $prismDll -Destination $stageDir

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force

    Remove-Item -LiteralPath $stageDir -Recurse -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
