#!/bin/sh
# Launches Disco Elysium with BepInEx and Non-Visual Calculus on an Apple Silicon Mac, as the game's
# native arm64 slice. Point Steam at the app bundle beside this script: right-click the game,
# Properties, General, Launch Options:
#
#   "/path/to/Disco Elysium/NonVisualCalculus.app" %command%
#
# install.sh prints that line with the real path. The script expects the loader unzipped beside
# disco.app: libdoorstop.dylib, dotnet/ and BepInEx/.
#
# Why a script and a bundle:
#
# - Steam on macOS does not run launch options through a shell, so the loader's DYLD_* and
#   DOORSTOP_* variables can only be set by a program it starts. %command% expands to the .app
#   bundle, so the script resolves the executable from Info.plist's CFBundleExecutable.
# - Steam on Apple Silicon spawns a launch-option command asking for x86_64 first, so a script
#   launch option runs under Rosetta, or fails with "Failed to spawn process" where Rosetta is
#   absent. An app bundle is opened through LaunchServices instead, which runs it natively, so the
#   launch option names NonVisualCalculus.app, whose only content runs this script with --open.
# - The bundle must not exec the game itself: a process LaunchServices started as one bundle that
#   then becomes another never gets keyboard focus. open(1) starts the game's own bundle the way
#   Steam does, as the arm64 slice, carrying the loader's variables (open --env) and every Steam*
#   variable so the Steam API finds its app id and user, and waits for the game to quit so Steam
#   keeps showing it as running.
# - dyld drops DYLD_* variables when a platform binary such as arch(1) is in between, so the
#   direct (non --open) path passes them with arch -e.
#
# The loader: Doorstop (libdoorstop.dylib, inserted by dyld) hooks il2cpp_init and boots the .NET
# runtime in dotnet/, which runs BepInEx's IL2CPP preloader. GameAssembly.dylib lives in the
# bundle's Contents/Frameworks, which joins DYLD_LIBRARY_PATH so the runtime's by-name loads of
# "GameAssembly" find the image already in the process.
#
# Whatever goes wrong, the game starts: without the mod when the loader is missing or the Mac is not
# Apple Silicon.
set -eu

a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; game_dir=$(cd "$a" || exit 1; pwd -P)

launch=exec
if [ "${1:-}" = "--open" ]; then
    launch=open
    shift
fi
executable="${1:-}"
[ $# -gt 0 ] && shift
if [ -z "$executable" ]; then
    executable="$game_dir/disco.app"
fi
case "$executable" in
    *.app)
        inner="$(defaults read "$executable/Contents/Info" CFBundleExecutable)"
        executable="$executable/Contents/MacOS/$inner"
    ;;
esac
if [ ! -x "$executable" ]; then
    echo "game binary not found: $executable" >&2
    exit 1
fi
app="$(cd "$(dirname "$executable")/../.." && pwd -P)"

# Called as: start_unmodded "reason" "$@" (the game's own arguments follow the reason).
start_unmodded() {
    echo "$1; starting the game without the mod" >&2
    shift
    cd "$game_dir"
    if [ "$launch" = open ]; then
        exec open -W "$app"
    fi
    exec "$executable" "$@"
}

if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" != 1 ]; then
    start_unmodded "Non-Visual Calculus needs an Apple Silicon Mac" "$@"
fi
if [ ! -f "$game_dir/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ] || [ ! -f "$game_dir/libdoorstop.dylib" ] \
    || [ ! -f "$game_dir/dotnet/libcoreclr.dylib" ]; then
    start_unmodded "the loader is not installed beside disco.app" "$@"
fi

target="$game_dir/BepInEx/core/BepInEx.Unity.IL2CPP.dll"
coreclr="$game_dir/dotnet/libcoreclr.dylib"
corlib="$game_dir/dotnet"
library_path="$app/Contents/Frameworks:$game_dir/BepInEx/core:$game_dir${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
# Steam injects its overlay through DYLD_INSERT_LIBRARIES too (STEAM_DYLD_INSERT_LIBRARIES when it
# hands the launch to a script); keep it behind Doorstop.
inherited="${DYLD_INSERT_LIBRARIES:-${STEAM_DYLD_INSERT_LIBRARIES:-}}"
insert="$game_dir/libdoorstop.dylib${inherited:+:$inherited}"

cd "$game_dir"
if [ "$launch" = open ]; then
    set -- --arch arm64 \
        --env DOORSTOP_ENABLED=1 --env "DOORSTOP_TARGET_ASSEMBLY=$target" \
        --env DOORSTOP_IGNORE_DISABLED_ENV=0 \
        --env "DOORSTOP_CLR_RUNTIME_CORECLR_PATH=$coreclr" --env "DOORSTOP_CLR_CORLIB_DIR=$corlib" \
        --env "DYLD_INSERT_LIBRARIES=$insert" --env "DYLD_LIBRARY_PATH=$library_path"
    for name in $(env | sed -n 's/^\(Steam[A-Za-z0-9_]*\)=.*/\1/p; s/^\(NVC_[A-Za-z0-9_]*\)=.*/\1/p'); do
        set -- "$@" --env "$name=$(printenv "$name")"
    done
    exec open -W "$@" "$app"
fi

export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY="$target"
export DOORSTOP_IGNORE_DISABLED_ENV=0
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$coreclr"
export DOORSTOP_CLR_CORLIB_DIR="$corlib"
exec arch -arm64 -e DYLD_INSERT_LIBRARIES="$insert" -e DYLD_LIBRARY_PATH="$library_path" "$executable" "$@"
