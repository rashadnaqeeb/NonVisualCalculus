#!/bin/sh
# Installs Non-Visual Calculus beside Disco Elysium's disco.app on an Apple Silicon Mac. Run it from
# the unzipped release folder, in Terminal:
#
#   ./install.sh
#
# or, if the game is not in Steam's default library, pass the game folder (the one that CONTAINS
# disco.app):
#
#   ./install.sh "/path/to/Disco Elysium"
#
# It copies the loader, the mod and the launcher into the game folder, clears the quarantine flag
# macOS puts on downloaded libraries, and puts the launch option to paste into Steam on the
# clipboard, printing it too. Running it again over an existing install updates the mod and keeps
# your settings.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
game_dir="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Disco Elysium}"

if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" != 1 ]; then
    echo "Non-Visual Calculus on macOS needs an Apple Silicon Mac." >&2
    exit 1
fi
if [ ! -d "$game_dir/disco.app" ]; then
    echo "Disco Elysium was not found at: $game_dir" >&2
    echo "Pass the game folder (the one containing disco.app) as the argument:" >&2
    echo "  ./install.sh \"/path/to/Disco Elysium\"" >&2
    exit 1
fi

echo "Installing into: $game_dir"
# The loader and the mod are replaced whole so nothing of an older version lingers. BepInEx/config
# (your settings) and BepInEx/interop (generated from the game on first launch) are kept.
rm -rf "$game_dir/BepInEx/core" "$game_dir/BepInEx/licenses" "$game_dir/dotnet" \
    "$game_dir/BepInEx/plugins/NonVisualCalculus" "$game_dir/NonVisualCalculus.app"
mkdir -p "$game_dir"
cp -R "$here/game/." "$game_dir/"
chmod 755 "$game_dir/run-modded.sh" "$game_dir/NonVisualCalculus.app/Contents/MacOS/run-modded"

# A file that arrived in a browser download carries the quarantine flag, and cp keeps it; cleared so
# the game may load the libraries. Harmless where nothing is flagged.
xattr -dr com.apple.quarantine "$game_dir" 2>/dev/null || true

launch_option="\"$game_dir/NonVisualCalculus.app\" %command%"
copied=""
if printf '%s' "$launch_option" | pbcopy 2>/dev/null; then
    copied=" It is on the clipboard."
fi

echo ""
echo "Installed. One step remains, in Steam:"
echo "right-click Disco Elysium, Properties, General, Launch Options, and paste this line"
echo "(including the quotes).$copied"
echo ""
echo "$launch_option"
echo ""
echo "Then launch from Steam. The first modded launch prepares the mod for your copy of the game,"
echo "which takes a minute or two with a black screen; after that the mod announces itself at the"
echo "main menu. README.md in this folder has the details."
