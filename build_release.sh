#!/bin/sh
# Builds the macOS release zip, the twin of build_release.ps1:
#   releases/NonVisualCalculus-macOS-v<version>.zip
# holding install.sh, the macOS README, and game/ - the files that go beside disco.app: the loader
# (third_party/bepinex-macos), the Release plugin output under BepInEx/plugins/NonVisualCalculus
# with its audio assets, lang files, scene descriptions and libprism.dylib, and the launcher
# (run-modded.sh and NonVisualCalculus.app). Run on a Mac, from anywhere.
set -eu

repo="$(cd "$(dirname "$0")" && pwd)"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -1)"
[ -n "$version" ] || { echo "Could not read Version from Directory.Build.props" >&2; exit 1; }

loader_zip="$repo/third_party/bepinex-macos/BepInEx-Unity.IL2CPP-macos-arm64-nvc.zip"
prism="$repo/third_party/prism/macos"
host_out="$repo/src/NonVisualCalculus/bin/mac/Release/net6.0"
module_out="$repo/src/NonVisualCalculus.Module/bin/mac/Release/net6.0"
stage="$repo/obj/mac/release-stage"
zip_path="$repo/releases/NonVisualCalculus-macOS-v$version.zip"

for required in "$loader_zip" "$prism/libprism.dylib"; do
    [ -f "$required" ] || { echo "Required file not found: $required" >&2; exit 1; }
done

# The plugin folder is filled by sweeping *.dll from the output dirs, so a stale DLL there (an
# assembly the build no longer produces) would ship. Start them empty.
rm -rf "$host_out" "$module_out"
dotnet build "$repo/NonVisualCalculus.slnx" -c Release -v minimal -nologo
for required in "$host_out/NonVisualCalculus.dll" "$module_out/NonVisualCalculus.Module.dll"; do
    [ -f "$required" ] || { echo "Release build output not found: $required" >&2; exit 1; }
done

rm -rf "$stage"
game="$stage/game"
plugin="$game/BepInEx/plugins/NonVisualCalculus"
mkdir -p "$plugin" "$repo/releases"

unzip -q "$loader_zip" -d "$game"
cp "$host_out/"*.dll "$module_out/NonVisualCalculus.Module.dll" "$prism/libprism.dylib" "$plugin/"
# NAudio's Windows parts (the winmm output, WASAPI, ASIO, MIDI and the umbrella assembly's decoders)
# never load on a Mac: the mod uses NAudio.Core there, with its own AudioQueue output.
rm -f "$plugin/NAudio.dll" "$plugin/NAudio.WinMM.dll" "$plugin/NAudio.Wasapi.dll" "$plugin/NAudio.Asio.dll" \
    "$plugin/NAudio.Midi.dll"
for set in cursor interactables walltones/1; do
    mkdir -p "$plugin/assets/audio/$set"
    cp "$repo/assets/audio/$set/"*.wav "$plugin/assets/audio/$set/"
done
mkdir -p "$plugin/lang"
cp "$repo/lang/"*.txt "$plugin/lang/"
mkdir -p "$plugin/descriptions"
cp "$repo/descriptions/"*.txt "$plugin/descriptions/"
mkdir -p "$game/BepInEx/licenses/prism"
cp "$prism/NOTICE" "$game/BepInEx/licenses/prism/NOTICE"
cp -R "$prism/LICENSES" "$game/BepInEx/licenses/prism/LICENSES"

install -m 755 "$repo/macos/run-modded.sh" "$game/run-modded.sh"
cp -R "$repo/macos/NonVisualCalculus.app" "$game/NonVisualCalculus.app"
chmod 755 "$game/NonVisualCalculus.app/Contents/MacOS/run-modded"
install -m 755 "$repo/macos/install.sh" "$stage/install.sh"
cp "$repo/macos/README.md" "$stage/README.md"

rm -f "$zip_path"
(cd "$stage" && zip -qry "$zip_path" .)
rm -rf "$stage"
echo "Release zip: $zip_path"
