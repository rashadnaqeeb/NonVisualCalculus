#!/usr/bin/env bash
# Full rebuild of the Ghidra IL2CPP reference. Run once after a game update (the binary changes).
# Heavy: Il2CppDumper, a full Ghidra auto-analysis pass, signature/type application, and a full
# decompile of every type (tens of minutes total). Produces decompiled/ghidra/ and refreshes the
# saved project so tools/re/decompile.sh queries are fast.
#
#   tools/re/refresh.sh                       # uses the default Steam install path
#   tools/re/refresh.sh "/c/path/to/Disco Elysium"   # or an explicit game folder
set -euo pipefail

GAME="${1:-${DISCO_ELYSIUM_DIR:-/c/Program Files (x86)/Steam/steamapps/common/Disco Elysium}}"
DLL="$GAME/GameAssembly.dll"
META="$GAME/disco_Data/il2cpp_data/Metadata/global-metadata.dat"
[ -f "$DLL" ] || { echo "GameAssembly.dll not found at: $DLL" >&2; exit 1; }

cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"   # tools/re

# Ghidra can't run from the spaced repo path; everything goes through the C:\disco-re junction.
MSYS_NO_PATHCONV=1 cmd /c 'C:\Users\rasha\Documents\Disco Elysium\tools\re\mkjunction.bat'

export JAVA_HOME='C:\disco-re\tools\re\jdk\jdk-21.0.11+10'
export GHIDRA_HEADLESS_MAXMEM=8G
HEADLESS='/c/disco-re/tools/re/ghidra/ghidra_12.1.2_PUBLIC/support/analyzeHeadless.bat'
B='C:\disco-re\tools\re'

echo "[1/5] Il2CppDumper (symbol map, structs) ..."
mkdir -p in out/dumper project
cp "$DLL" in/GameAssembly.dll
# Il2CppDumper's trailing "press any key" throws without a console; the dump finishes first, so ignore it.
./il2cppdumper/Il2CppDumper.exe "$DLL" "$META" out/dumper || true
[ -f out/dumper/script.json ] || { echo "Il2CppDumper did not produce script.json" >&2; exit 1; }
( cd out/dumper && python ../../il2cppdumper/il2cpp_header_to_ghidra.py )   # -> il2cpp_ghidra.h

echo "[2/5] Ghidra import + auto-analysis + method names (the long one) ..."
MSYS_NO_PATHCONV=1 "$HEADLESS" "$B\\project" GameAssembly -overwrite \
  -import "$B\\in\\GameAssembly.dll" -scriptPath "$B\\scripts" \
  -postScript ApplyIl2Cpp.java "$B\\out\\dumper\\script.json"

echo "[3/5] type + signature application (named struct fields) ..."
MSYS_NO_PATHCONV=1 "$HEADLESS" "$B\\project" GameAssembly -process GameAssembly.dll -noanalysis \
  -scriptPath "$B\\scripts" \
  -postScript ApplyStructs.java "$B\\out\\dumper\\script.json" "$B\\out\\dumper\\il2cpp_ghidra.h"

echo "[4/5] global string legend ..."
mkdir -p ../../decompiled/ghidra
python - "$PWD/out/dumper/script.json" "$PWD/../../decompiled/ghidra/_strings.txt" <<'PY'
import io, json, sys
d = json.load(open(sys.argv[1], encoding='utf-8'))
with io.open(sys.argv[2], 'w', encoding='utf-8') as o:
    for i, s in enumerate(d.get('ScriptString', []), 1):
        o.write(u'StringLiteral_%d\t%s\n' % (i, (s.get('Value') or '').replace('\r','').replace('\n','\\n')))
PY

echo "[5/5] full per-type decompile -> decompiled/ghidra/ ..."
MSYS_NO_PATHCONV=1 "$HEADLESS" "$B\\project" GameAssembly -process GameAssembly.dll -noanalysis \
  -scriptPath "$B\\scripts" -postScript FullExport.java 'C:\disco-re\decompiled\ghidra'

echo "done. Reference rebuilt in decompiled/ghidra/; query a class with tools/re/decompile.sh <Type\$\$>."
