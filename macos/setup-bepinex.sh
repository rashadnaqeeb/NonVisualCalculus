#!/bin/sh
# Installs the vendored macOS loader (third_party/bepinex-macos) beside disco.app, the Mac twin of
# setup-bepinex.ps1. Idempotent: safe to re-run (e.g. after a game update or a loader rebuild).
# It also installs the launcher (run-modded.sh and NonVisualCalculus.app). After this, start the game
# once with run-modded.sh in the game folder (Steam running): the first modded launch generates
# BepInEx/interop, the proxy assemblies the build compiles against (a minute or two; the game window
# stays black meanwhile). Then `dotnet build` deploys the mod.
#
#   macos/setup-bepinex.sh [game folder]
#
# The game folder is the one CONTAINING disco.app; it defaults to DISCO_ELYSIUM_DIR, then Steam's
# default library.
set -eu

repo="$(cd "$(dirname "$0")/.." && pwd)"
zip="$repo/third_party/bepinex-macos/BepInEx-Unity.IL2CPP-macos-arm64-nvc.zip"
game="${1:-${DISCO_ELYSIUM_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Disco Elysium}}"

if [ ! -d "$game/disco.app" ]; then
    echo "Disco Elysium not found at: $game" >&2
    echo "Pass the folder containing disco.app, or set DISCO_ELYSIUM_DIR." >&2
    exit 1
fi
if [ ! -f "$zip" ]; then
    echo "The loader zip is missing: $zip (build it with macos/loader/build-loader.sh)" >&2
    exit 1
fi

# The loader's own files are replaced whole so nothing of an older build lingers; config, plugins
# and the generated interop assemblies are kept.
rm -rf "$game/BepInEx/core" "$game/dotnet" "$game/libdoorstop.dylib"
unzip -q -o "$zip" -d "$game"
install -m 755 "$repo/macos/run-modded.sh" "$game/run-modded.sh"
rm -rf "$game/NonVisualCalculus.app"
cp -R "$repo/macos/NonVisualCalculus.app" "$game/NonVisualCalculus.app"
xattr -dr com.apple.quarantine "$game" 2>/dev/null || true
echo "Loader installed into: $game"
