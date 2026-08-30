#!/usr/bin/env bash
# Decompile every il2cpp method whose name contains <query> into decompiled/ghidra/<query>.c
# (non-alphanumerics become `_`, so 'SenseOrb$$' writes SenseOrb__.c),
# reading from the already-analyzed Ghidra project (no re-analysis). Seconds, not minutes.
#
#   tools/re/decompile.sh 'SenseOrb$$'      # every method of SenseOrb ($$ separates Type from Method)
#   tools/re/decompile.sh 'DialogueManager$$Bark'   # any name substring also works
#
# Output includes a legend resolving StringLiteral_N references to their text.
# Requires the analyzed project from tools/re/refresh.sh (see tools/re/README.md).
set -euo pipefail

QUERY="${1:?usage: decompile.sh <name substring, e.g. SenseOrb\$\$>}"
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
[ -d "$RE_HOME/project" ] || { echo "No analyzed project at $RE_HOME/project; run tools/re/refresh.sh first." >&2; exit 1; }

mkdir -p "$REPO/decompiled/ghidra"

headless "$PROJECT_WIN" GameAssembly -process GameAssembly.dll -noanalysis \
  -scriptPath "$SCRIPTS_WIN" \
  -postScript ExportDecompiled.java "$QUERY" "$GHIDRA_OUT_WIN" \
  2>&1 | grep -E "ExportDecompiled|ERROR|Exception" || true
