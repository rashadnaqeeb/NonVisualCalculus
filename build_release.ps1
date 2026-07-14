# Build the distributable mod zips: the vendored BepInEx game-folder layout, the Release
# plugin output under BepInEx\plugins\NonVisualCalculus, the audio assets and lang files
# beside the plugin, and prism.dll at the game-folder root. The zip root IS the game
# folder, so the installer (and a manual user) extracts it straight into the game dir.
#
# Two zips come out. NonVisualCalculus-v<version>.zip is the release. WhirlingInWords-v<version>.zip
# is the same release under the mod's pre-rename asset name, for installer exes from the
# Whirling in Words era still on players' machines: they only recognize that asset name, and they
# never delete files, only overwrite. So the compat zip adds a zero-byte file at every path the
# old releases put under BepInEx\plugins\WhirlingInWords (the frozen 1.0.0/1.0.1 file list):
# overwriting the old plugin with empty files kills it (BepInEx skips an unreadable DLL, and a
# zero-byte file can never satisfy an assembly lookup the way a named stub assembly could), and
# listing them in that installer's manifest keeps its uninstall able to remove them.

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
$hostOutDir = Join-Path $scriptDir "src\NonVisualCalculus\bin\Release\net6.0"
$moduleDll = Join-Path $scriptDir "src\NonVisualCalculus.Module\bin\Release\net6.0\NonVisualCalculus.Module.dll"
$zipPath = Join-Path $releaseDir "NonVisualCalculus-v$version.zip"
$compatZipPath = Join-Path $releaseDir "WhirlingInWords-v$version.zip"

# Every file the 1.0.0/1.0.1 releases shipped under BepInEx\plugins\WhirlingInWords. Frozen: no
# release will ever add to the old folder again, so this list never changes.
$legacyPluginFiles = @(
    "Microsoft.CodeAnalysis.CSharp.Scripting.dll"
    "Microsoft.CodeAnalysis.CSharp.dll"
    "Microsoft.CodeAnalysis.Scripting.dll"
    "Microsoft.CodeAnalysis.dll"
    "NAudio.Asio.dll"
    "NAudio.Core.dll"
    "NAudio.Midi.dll"
    "NAudio.Wasapi.dll"
    "NAudio.WinMM.dll"
    "NAudio.dll"
    "System.Collections.Immutable.dll"
    "System.Reflection.Metadata.dll"
    "WhirlingInWords.Core.dll"
    "WhirlingInWords.Module.dll"
    "WhirlingInWords.dll"
    "assets\audio\cursor\cursor_impassable.wav"
    "assets\audio\cursor\enter.wav"
    "assets\audio\cursor\exit.wav"
    "assets\audio\cursor\fog_enter.wav"
    "assets\audio\cursor\fog_exit.wav"
    "assets\audio\interactables\container.wav"
    "assets\audio\interactables\door.wav"
    "assets\audio\interactables\door_open.wav"
    "assets\audio\interactables\interactable.wav"
    "assets\audio\interactables\npc.wav"
    "assets\audio\interactables\orb.wav"
    "assets\audio\walltones\1\east.wav"
    "assets\audio\walltones\1\north.wav"
    "assets\audio\walltones\1\south.wav"
    "assets\audio\walltones\1\west.wav"
    "lang\ar.txt"
    "lang\de.txt"
    "lang\en.txt"
    "lang\es.txt"
    "lang\fr.txt"
    "lang\ja.txt"
    "lang\ko.txt"
    "lang\pl.txt"
    "lang\pt-br.txt"
    "lang\ru.txt"
    "lang\tr.txt"
    "lang\zh-tw.txt"
    "lang\zh.txt"
)

foreach ($required in @($bepinexZip, $prismDll)) {
    if (-not (Test-Path $required)) {
        throw "Required file not found: $required"
    }
}

Push-Location $scriptDir
try {
    # The zip is built by sweeping *.dll from the output dirs, so a stale DLL there (a renamed
    # or removed assembly dotnet build no longer owns) would ship. Start them empty.
    foreach ($outDir in @($hostOutDir, (Split-Path -Parent $moduleDll))) {
        if (Test-Path $outDir) {
            Remove-Item -LiteralPath $outDir -Recurse -Force
        }
    }

    dotnet build NonVisualCalculus.slnx -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    $pluginDll = Join-Path $hostOutDir "NonVisualCalculus.dll"
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

    $pluginDir = Join-Path $stageDir "BepInEx\plugins\NonVisualCalculus"
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

    # The compat zip: the same stage plus the zero-byte tombstones over the old plugin folder.
    $legacyDir = Join-Path $stageDir "BepInEx\plugins\WhirlingInWords"
    foreach ($rel in $legacyPluginFiles) {
        $tombstone = Join-Path $legacyDir $rel
        New-Item -ItemType Directory -Force (Split-Path -Parent $tombstone) | Out-Null
        New-Item -ItemType File -Force $tombstone | Out-Null
    }
    if (Test-Path $compatZipPath) {
        Remove-Item -LiteralPath $compatZipPath -Force
    }
    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $compatZipPath -Force

    Remove-Item -LiteralPath $stageDir -Recurse -Force

    Write-Host "Release zip: $zipPath"
    Write-Host "Compat zip (pre-rename installers): $compatZipPath"
}
finally {
    Pop-Location
}
