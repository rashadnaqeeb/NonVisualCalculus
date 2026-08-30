# Ghidra IL2CPP decompile pipeline

Recovers real method bodies from `GameAssembly.dll` as readable C, with il2cpp method names, named
struct fields (`this->fields.textOverride`), typed parameters, cross-references, and string literals
resolved to their text. This is ground truth for how the game works, complementing
`decompiled/dummydll` + `decompiled/src` (signatures only, empty bodies) and Cpp2IL's
`dll_il_recovery` (near-C# but drops/mangles a fraction of methods).

Use it to answer "what does this method actually do", "who calls this", and "what text does this
return" without probing the live game.

## Two artifacts

- `decompiled/ghidra/` - the full pre-decompiled reference: one `.c` per type under namespace dirs
  (15k+ files), plus `_strings.txt` mapping every `StringLiteral_N` to its text. Browse and grep this.
- The saved Ghidra project under `C:\disco-re\project\` - the analyzed+typed database. `decompile.sh`
  queries it on demand for a fresh single-class dump.

## Layout

Only the scripts and this README are tracked. Everything heavy lives outside the repo in a local,
space-free folder, `C:\disco-re` by default (override with `NVC_RE_HOME`, POSIX form): Ghidra's
batch launcher rejects paths containing spaces, its database does heavy random I/O that crawls over
the Mac share the repo sits on, and that share allows no junctions to fake a local path. Ghidra reads
the scripts straight from the repo and writes the exported tree back into it; everything else stays
on C:.

In the repo (`tools/re/`):

- `env.sh` - shared locations (tool folder, JDK, Windows-form paths for the .bat launcher).
- `scripts/ApplyIl2Cpp.java` - post-analysis: names every method, labels string literals.
- `scripts/ApplyStructs.java` - parses the header and applies per-method signatures so struct
  fields get names; idempotent (skips the header parse if types are already present).
- `scripts/ExportDecompiled.java` - decompiles functions matching a query, with a string legend.
- `scripts/FullExport.java` - decompiles every type to the `decompiled/ghidra/` tree, in parallel.
- `decompile.sh` / `refresh.sh` - the Ghidra drivers.
- `dump-cpp2il.sh` - the signature-only reference (`decompiled/dummydll` via `tools/Cpp2IL.exe`,
  `decompiled/src` via `ilspycmd`), independent of Ghidra.

In `C:\disco-re`:

- `ghidra/ghidra_12.1.2_PUBLIC/`, `il2cppdumper/` - the tools (Ghidra 12.1.2, Il2CppDumper 6.7.46
  including its `il2cpp_header_to_ghidra.py`).
- `in/GameAssembly.dll` - a copy of the game binary (Ghidra import source).
- `out/dumper/` - Il2CppDumper output: `script.json` (address to name map), `il2cpp.h`,
  `il2cpp_ghidra.h` (inheritance flattened for Ghidra's C parser), `dump.cs`.
- `project/` - the saved Ghidra project (the analyzed, named, and typed database).
- `dl/` - the downloaded archives.

Java comes from `JAVA_HOME`, else the `java` on `PATH`. It must be an x64 JDK 21+ even on the ARM64
VM: Ghidra ships only x64 native decompiler binaries for Windows and picks them by the JVM's
`os.arch`, so an ARM64 JDK finds no decompiler. Everything then runs under x64 emulation, which
works but makes the full analysis slower than on native hardware.

## Daily use

Browse or grep `decompiled/ghidra/` directly. For a fresh dump of one class (seconds, queries the
saved project):

    tools/re/decompile.sh 'SenseOrb$$'        # $$ separates Type from Method
    tools/re/decompile.sh 'DialogueManager$$' # any function-name substring works too

Output lands in `decompiled/ghidra/<query>.c` (non-alphanumerics in the query become `_`, so
`SenseOrb$$` writes `SenseOrb__.c`) with a string-literal legend at the top.

## After a game update (about yearly)

The binary changes, so rebuild everything (Il2CppDumper, a full Ghidra analysis pass, type
application, and the full decompile - tens of minutes total, longer under emulation), and the
signature-only reference:

    tools/re/refresh.sh
    tools/re/dump-cpp2il.sh

Both default to Steam's main-library install; pass the game folder as the first argument or set
`DISCO_ELYSIUM_DIR` otherwise. Runs over ten minutes belong in the background with output to a log.

## Known limits

- IL2CPP shares one native function across many generic instantiations, so ~98k native functions
  back ~170k logical methods; a shared body is filed under just one of its methods.
- ~2k of 170k signatures and ~9 of 98k function decompiles fail on edge cases (unusual generics);
  those show raw offsets or a `// FAILED` line. Everything else has named fields.
- Field/struct typing comes from `il2cpp_ghidra.h`; if a game update changes the metadata version,
  re-running `refresh.sh` regenerates it.

## One-time setup (reproduce on a fresh machine)

1. Install an x64 JDK 21+ (Temurin) so `java` is on `PATH`, or point `JAVA_HOME` at one.
2. Download `ghidra_12.1.2_PUBLIC_<date>.zip` from the Ghidra GitHub releases and unzip it to
   `C:\disco-re\ghidra\` (giving `C:\disco-re\ghidra\ghidra_12.1.2_PUBLIC\`).
3. Download `Il2CppDumper-win-v6.7.46.zip` from the Il2CppDumper GitHub releases and unzip it to
   `C:\disco-re\il2cppdumper\` (`Il2CppDumper.exe` and `il2cpp_header_to_ghidra.py` land there).
4. Python 3 on `PATH` (the header converter and the string legend use it); `ilspycmd` as a global
   dotnet tool for `dump-cpp2il.sh`.
5. Run `tools/re/refresh.sh` and `tools/re/dump-cpp2il.sh`.
