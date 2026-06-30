#!/usr/bin/env bash
# Decompile every il2cpp method whose name contains <query> into decompiled/ghidra/<query>.c,
# reading from the already-analyzed Ghidra project (no re-analysis). Seconds, not minutes.
#
#   tools/re/decompile.sh 'SenseOrb$$'      # every method of SenseOrb ($$ separates Type from Method)
#   tools/re/decompile.sh 'DialogueManager$$Bark'   # any name substring also works
#
# Output includes a legend resolving StringLiteral_N references to their text.
# Requires the one-time setup in tools/re/README.md (Ghidra project must already exist).
set -euo pipefail

QUERY="${1:?usage: decompile.sh <name substring, e.g. SenseOrb\$\$>}"
JDK='C:\disco-re\tools\re\jdk\jdk-21.0.11+10'
HEADLESS='/c/disco-re/tools/re/ghidra/ghidra_12.1.2_PUBLIC/support/analyzeHeadless.bat'
B='C:\disco-re\tools\re'

# Ensure the space-free junction exists (Ghidra can't run from the spaced repo path).
if [ ! -e /c/disco-re/tools/re/scripts/ExportDecompiled.java ]; then
  MSYS_NO_PATHCONV=1 cmd /c 'C:\Users\rasha\Documents\Disco Elysium\tools\re\mkjunction.bat'
fi

mkdir -p "$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/decompiled/ghidra"

export JAVA_HOME="$JDK"
MSYS_NO_PATHCONV=1 "$HEADLESS" \
  "$B\\project" GameAssembly -process GameAssembly.dll -noanalysis \
  -scriptPath "$B\\scripts" \
  -postScript ExportDecompiled.java "$QUERY" 'C:\disco-re\decompiled\ghidra' \
  2>&1 | grep -E "ExportDecompiled|ERROR|Exception" || true
