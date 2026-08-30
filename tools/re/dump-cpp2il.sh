#!/usr/bin/env bash
# Rebuild the signature-only reference: decompiled/dummydll/ (Cpp2IL stub assemblies, accurate
# signatures, empty method bodies) and decompiled/src/<Assembly>/ (ilspy C#, one file per type,
# directories mirroring namespaces) for the game assemblies the mod works against. Run once after
# a game update. For real method bodies use tools/re/refresh.sh (Ghidra) instead.
#
#   tools/re/dump-cpp2il.sh                                # uses the default Steam install path
#   tools/re/dump-cpp2il.sh "/c/path/to/Disco Elysium"     # or an explicit game folder
set -euo pipefail

GAME="${1:-${DISCO_ELYSIUM_DIR:-/c/Program Files (x86)/Steam/steamapps/common/Disco Elysium}}"
[ -f "$GAME/GameAssembly.dll" ] || { echo "GameAssembly.dll not found in: $GAME" >&2; exit 1; }

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$REPO/decompiled"
ASSEMBLIES=(Assembly-CSharp DialogueSystem PixelCrushers l2Localization)

echo "[1/2] Cpp2IL stub assemblies -> decompiled/dummydll/ ..."
rm -rf "$OUT/dummydll"
"$REPO/tools/Cpp2IL.exe" --game-path "$GAME" --output-as dummydll --output-to "$OUT/dummydll"
[ -f "$OUT/dummydll/Assembly-CSharp.dll" ] || { echo "Cpp2IL did not produce dummydll/Assembly-CSharp.dll" >&2; exit 1; }

echo "[2/2] ilspy C# per type -> decompiled/src/<Assembly>/ ..."
for a in "${ASSEMBLIES[@]}"; do
  rm -rf "$OUT/src/$a"
  ilspycmd --disable-updatecheck --ignore-decompilation-errors --nested-directories \
    -p -r "$OUT/dummydll" -o "$OUT/src/$a" "$OUT/dummydll/$a.dll"
done

echo "done. Signatures in decompiled/dummydll/ and decompiled/src/."
