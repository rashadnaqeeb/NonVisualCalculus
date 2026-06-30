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
- The saved Ghidra project under `tools/re/project/` - the analyzed+typed database. `decompile.sh`
  queries it on demand for a fresh single-class dump.

## Layout

Everything heavy is vendored under `tools/re/` and gitignored; only the scripts and this README are
tracked.

- `jdk/`, `ghidra/`, `il2cppdumper/` - vendored tools (JDK 21, Ghidra 12.1.2, Il2CppDumper 6.7.46).
- `in/GameAssembly.dll` - a space-free copy of the game binary (Ghidra import source).
- `out/dumper/` - Il2CppDumper output: `script.json` (address to name map), `il2cpp.h`,
  `il2cpp_ghidra.h` (inheritance flattened for Ghidra's C parser), `dump.cs`.
- `project/` - the saved Ghidra project (the analyzed, named, and typed database).
- `scripts/ApplyIl2Cpp.java` - post-analysis: names every method, labels string literals.
- `scripts/ApplyStructs.java` - parses the header and applies per-method signatures so struct
  fields get names; idempotent (skips the header parse if types are already present).
- `scripts/ExportDecompiled.java` - decompiles functions matching a query, with a string legend.
- `scripts/FullExport.java` - decompiles every type to the `decompiled/ghidra/` tree, in parallel.
- `decompile.sh` / `refresh.sh` / `mkjunction.bat` - drivers.

## The junction

Ghidra's batch launcher breaks on the space in "Disco Elysium". `mkjunction.bat` creates
`C:\disco-re` as a directory junction (no admin needed) giving a space-free view of the repo; all
tooling runs through it. The drivers recreate it automatically if missing.

## Daily use

Browse or grep `decompiled/ghidra/` directly. For a fresh dump of one class (seconds, queries the
saved project):

    tools/re/decompile.sh 'SenseOrb$$'        # $$ separates Type from Method
    tools/re/decompile.sh 'DialogueManager$$' # any function-name substring works too

Output lands in `decompiled/ghidra/<query>.c` with a string-literal legend at the top.

## After a game update (about yearly)

The binary changes, so rebuild everything (Il2CppDumper, a full Ghidra analysis pass, type
application, and the full decompile - tens of minutes total):

    tools/re/refresh.sh

## Known limits

- IL2CPP shares one native function across many generic instantiations, so ~98k native functions
  back ~170k logical methods; a shared body is filed under just one of its methods.
- ~2k of 170k signatures and ~9 of 98k function decompiles fail on edge cases (unusual generics);
  those show raw offsets or a `// FAILED` line. Everything else has named fields.
- Field/struct typing comes from `il2cpp_ghidra.h`; if a game update changes the metadata version,
  re-running `refresh.sh` regenerates it.

## One-time setup (already done; reproduce on a fresh machine)

Download JDK 21 (Temurin), Ghidra 12.1.2, Il2CppDumper 6.7.46 into `tools/re/{jdk,ghidra,il2cppdumper}`,
then run `tools/re/refresh.sh`.
