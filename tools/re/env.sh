# Shared locations for the Ghidra IL2CPP pipeline. Sourced by refresh.sh and decompile.sh.
#
# The heavy, gitignored parts (Ghidra, Il2CppDumper, the analyzed project database) live in a
# local space-free folder on C:, not in the repo: Ghidra's batch launcher rejects paths containing
# spaces, its database does heavy random I/O that crawls over the Mac share, and the share allows
# no junctions to fake a local path. Only the scripts (read from the repo) and the exported
# decompiled/ghidra/ tree (written into the repo) cross that boundary.
#
#   NVC_RE_HOME   local tool folder, POSIX form   (default /c/disco-re)
#   JAVA_HOME     a JDK 21+ for Ghidra; defaults to the java on PATH. Must be an x64 JDK even on
#                 Windows ARM64: Ghidra ships only x64 native decompiler binaries for Windows and
#                 picks them by the JVM's os.arch, so an ARM64 JDK finds no decompiler.

RE_HOME="${NVC_RE_HOME:-/c/disco-re}"
RE_HOME_WIN="$(cygpath -w "$RE_HOME")"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
REPO_WIN="$(cygpath -w "$REPO")"

GHIDRA_VERSION=12.1.2
HEADLESS="$RE_HOME/ghidra/ghidra_${GHIDRA_VERSION}_PUBLIC/support/analyzeHeadless.bat"
DUMPER="$RE_HOME/il2cppdumper/Il2CppDumper.exe"
HEADER_TO_GHIDRA="$RE_HOME/il2cppdumper/il2cpp_header_to_ghidra.py"

# Windows-form paths handed to the .bat launcher (cmd.exe cannot take POSIX paths).
PROJECT_WIN="$RE_HOME_WIN\\project"
SCRIPTS_WIN="$REPO_WIN\\tools\\re\\scripts"
DUMPER_OUT_WIN="$RE_HOME_WIN\\out\\dumper"
GHIDRA_OUT_WIN="$REPO_WIN\\decompiled\\ghidra"

if [ -z "${JAVA_HOME:-}" ]; then
  JAVA_BIN="$(command -v java || true)"
  [ -n "$JAVA_BIN" ] || { echo "No java on PATH and JAVA_HOME unset; install an x64 JDK 21+." >&2; exit 1; }
  JAVA_HOME="$(cygpath -w "$(dirname "$(dirname "$JAVA_BIN")")")"
fi
export JAVA_HOME
export GHIDRA_HEADLESS_MAXMEM="${GHIDRA_HEADLESS_MAXMEM:-8G}"

[ -f "$HEADLESS" ] || { echo "Ghidra $GHIDRA_VERSION not found at $HEADLESS; see tools/re/README.md (setup)." >&2; exit 1; }

# Run analyzeHeadless with the given arguments (all path arguments in Windows form).
headless() {
  MSYS_NO_PATHCONV=1 "$HEADLESS" "$@"
}
