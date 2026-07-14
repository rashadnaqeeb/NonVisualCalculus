# Whirling in Words - Claude Code Instructions

Whirling in Words (code identifier `WhirlingInWords`, named after the game's Whirling-in-Rags)
makes **Disco Elysium (The Final Cut)** playable by blind users. Speech is the sole
interface, so if something fails silently, speaks stale data, or omits information, the player has no
way to know. A logged failure is actionable; a silent one is invisible.

The game is a dialogue-heavy isometric RPG with no combat and no real-time action, so the work splits
the way our reference mods did: a **UI layer** (menus, dialogue, inventory, thought cabinet, character
sheet) read by hooking the game's focus system and announcing, and a **world layer** (the isometric
scene you move through and click) read with a cursor plus a scanner of interactables. We ride the
game's own controller-navigation and pathfinding.

## Game & environment

- Engine: **Unity 2020.3.12f1, IL2CPP, x64** (build 2022-05-20, ZAUM). Game code is native
  (`GameAssembly.dll` + `il2cpp_data/Metadata/global-metadata.dat`), so we work through Il2CppInterop proxies.
- Install: the build resolves the game folder itself (`GameDir`), so no machine path is committed - it
  reads Steam's main library from the registry, with overrides via `-p:GameDir=...`, the
  `DISCO_ELYSIUM_DIR` env var, or a gitignored `Directory.Build.local.props` (see `Directory.Build.props`).
  For an install outside Steam's main library, set one of those overrides. The game must
  be launched **through Steam**; running `disco.exe` directly exits on the DRM check.
- Loader: **BepInEx 6.0.0-pre.2, Unity.IL2CPP win-x64** (vendored in `third_party/bepinex/`). It runs
  Il2CppInterop on first launch to generate managed proxy assemblies under `<game>\BepInEx\interop\` -
  those are our compile targets (Il2Cpp-prefixed types). Bundles HarmonyX. Mod assemblies run on
  **.NET 6** (BepInEx's bundled CoreCLR), so the plugin targets `net6.0`.
- Middleware: UI is **uGUI + TextMeshPro**; dialogue is the **Pixel Crushers Dialogue System**
  (`DialogueSystem.dll`, `PixelCrushers.dll`); localization is **I2** (`l2Localization.dll`). DE wraps
  much of its UI in a custom "Sunshine" / "Pages" framework (`PagesSystem`, `Pages.Gameplay.*`, `SubPage`).
- Speech backend is **Prism** (https://github.com/ethindp/prism), bound via hand-written P/Invoke
  against `prism.dll`, vendored in `third_party/prism/` and deployed next to `disco.exe`.
- Logs: our lines go through BepInEx logging with a `[Whirling in Words]` prefix into
  `<game>\BepInEx\LogOutput.log` (truncated each launch).

## Decompiled reference

`decompiled/` (gitignored) holds the game's class surface for lookup: `dummydll/` (Cpp2IL stub DLLs)
and `src/` (ilspy C# per type - Assembly-CSharp, DialogueSystem, PixelCrushers, l2Localization). Look
up any game type/method/field signature here before guessing.

**Caveat: the Cpp2IL dummy DLLs have accurate signatures but EMPTY method bodies** (everything returns
`false`/`default`/`null`). Read structure from them, never logic. For real behavior, prefer the Ghidra
pipeline (next paragraph); failing that, re-dump targeted classes with Cpp2IL's IL-recovery mode
(`tools/Cpp2IL.exe`), read the public Pixel Crushers docs, or confirm live with debug logging.

**Real method bodies via Ghidra (`tools/re/`).** The game binary is decompiled to readable C with
il2cpp method names, named struct fields (`this->fields.textOverride`), typed parameters, call
targets, and string literals resolved to their text. **Prefer reading this over probing the live game
through the dev HTTP server** when working out how a method behaves, where a hook belongs, or what text
a call returns: the decompiled code is ground truth read in one shot, whereas a live probe only samples
one hypothesis at a time and leaves uncertainty. Use the HTTP server to confirm runtime values, not to
discover structure. Two artifacts: the full pre-decompiled tree at `decompiled/ghidra/` (one `.c` per
type under namespace dirs, plus `_strings.txt`) to browse and grep, and the saved Ghidra project for
fresh single-class dumps via `tools/re/decompile.sh 'Type$$'` (the `$$` separates Type from Method,
e.g. `SenseOrb$$`), which writes `decompiled/ghidra/<query>.c`. After a game update run
`tools/re/refresh.sh` once to rebuild everything. See `tools/re/README.md`.

**Accessibility in `decompiled/` is not the proxy's.** The decompiled sources report each method's
original accessibility, but the Il2CppInterop proxies we actually compile against
(`<game>/BepInEx/interop/`) generate most members public. A method shown `private` in `decompiled/`
(e.g. `QuicktravelController.IsQuicktravelAvailable()`) is usually still callable from
`WhirlingInWords.Module` and the dev REPL, so don't conclude "private, won't compile" from the source;
verify by building (`dotnet build`) or calling it in the REPL. To check a proxy method is bound to
native rather than returning a stub default, call a sibling whose true value differs from the default
(`IsOutside()` returning true proved the binding works).

## Build & deploy

`dotnet build` is the whole loop. The `WhirlingInWords` project has a post-build target (Debug only) that
copies `WhirlingInWords.dll` + `WhirlingInWords.Core.dll` into `<GameDir>\BepInEx\plugins\WhirlingInWords\` and
`prism.dll` next to `disco.exe`, with `GameDir` auto-detected (see above). **Close the game first** or
the dll copy is skipped (file locked) and you'll run a stale build. `build.ps1` is just a wrapper for
`dotnet build`; deploy is automatic.

- `dotnet build WhirlingInWords.slnx -c Debug` - build all four projects and deploy.
- `dotnet test WhirlingInWords.slnx` - run the unit suite (Core only; no game, no Unity).
- `dotnet build -c Release` compiles without deploying.

**Build Debug to test.** Always build Debug (the default) when verifying a change - only Debug deploys,
so `-c Release` proves compilation but leaves a stale deployed build and an untested change; reach for
it only for a deliberate non-deploying compile. If a Debug deploy is skipped because the running game
has the DLLs locked (`MSB3021` "being used by another process") and the change needs a restart to take
effect (any `WhirlingInWords.Core` or host change, which can't hot-reload), carry the whole cycle yourself
rather than handing back a "you need to relaunch": close the game, re-run `dotnet build` so the deploy
lands, then relaunch through Steam (kill/launch commands under Gotchas). A pure-module change needs no
restart (F6 / `POST /reload`).

**Git.** Commits land on `main` by default; if a session already started on a feature branch keep
working there, and when a feature branch we made is done, merge it to main (fast-forward when possible)
and delete it. This governs where a commit lands, not whether to commit - still only commit, merge, or
push when the user asks.

**Dev driver (in-process HTTP server + hot-reload) - for iteration, not a player feature.** A loopback
dev server is baked into the host (`Debug` only) and **on by default**, binding **127.0.0.1 only**
(reachable from this machine alone). Disable with `WHIRLING_NO_DEV=1`; set the port with
`WHIRLING_DEV_PORT` (default 8771). It lets an agent introspect and drive the live game over
`http://127.0.0.1:8771`.

Bring-up: launch through Steam (kill/launch commands under Gotchas), then poll
`curl -s --retry 60 --retry-connrefused --retry-delay 1 http://127.0.0.1:8771/health`. The dev server
forces `Application.runInBackground = true`, so the game keeps simulating while its window is unfocused
and you can drive it with your terminal focused.

Endpoints (loopback; drive with `curl`):
- `POST /eval` - body is C# source, compiled by **Roslyn** and run on the Unity main thread against the
  live game. REPL **state persists across calls** - define session helpers (a find-entity-by-name, a
  player-position shorthand) once and reuse them. References every interop proxy up front, so any game
  type resolves. Returns captured output, compile diagnostics, and exceptions (caught - eval errors
  never crash the game), then a `speech:` section with whatever the mod SPOKE as a consequence (it
  waits for a quiet window; `?speech=0` skips it, `?settle=MS` tunes it, default 250) - so act-then-
  listen is one request, no cursor bookkeeping.
- `POST /input` - body is a verb. UI verbs (`up|down|left|right|confirm|back|tab|prev|home|end|secondary`)
  drive **our own navigator** when it owns the keyboard (a migrated screen or the popup overlay, where
  DE's `NavigationManager` is muted), else fall back to DE's focus system for not-yet-migrated screens -
  both via the game's **own logical handlers**, never OS synthetic keys (a backgrounded window can't take
  those). World verbs (`interact|stop|recenter|scan-next|scan-prev|scan-category-next|scan-category-prev|
  scan-people[-prev]|scan-items[-prev]|scan-exits[-prev]|scan-interact`, or any raw registered key like
  `world.read.time`) fire the world reader's own
  handlers while it owns the keyboard - the real player path, so use these rather than `/eval`-calling
  game internals when testing world flows. Enter/Escape on a focused text field commit/cancel the edit
  first. Read results via `/speech` (or just use `/eval`'s speech capture).
- `POST /type` - body is text appended to the focused input field (e.g. a save name).
- `POST /wait?timeout=MS` - body is a C# bool expression, compiled in the /eval session (it can use
  variables earlier evals defined) and evaluated **every frame** on the main thread; returns when true
  or on timeout (default 10s). Use this instead of curl sleep-loops: per-frame sampling never misses a
  transient (e.g. `movementStatus` reads IDLE for frames before a walk engages, which external polling
  mistakes for arrival).
- `POST /reload` - rebuild the feature module from its freshly built DLL, no restart (same path as
  **F6**). Responds with the full `/module` readout - check the DLL write time (stale deploy?) and the
  patch table (all patches should be live).
- `GET /module` - the loaded module type + **load generation**, the module DLL's write time and age, and
  the process-wide **Harmony patch table with live counts**. A method listed "0 live" was patched then
  stripped - if a Harmony-driven feature (a notification, a bark, a container cue) goes silent, look
  here first.
- `GET /typeinfo?name=X` - find a type by simple name across everything loaded plus the interop
  proxies, returning full name, assembly, and (single match) its public members; enums list their
  values. Use this instead of guessing namespaces in the REPL (`MovementMode` is in `Sunshine`,
  `ViewsPagesBridge` is global); the decompiled tree also answers it (`decompiled/src` directories
  mirror namespaces) but slower.
- `GET /focus` - the current uGUI selection (EventSystem + `NavigationManager`, with the path and text),
  independent of speech, works even if the module failed to load or its `Tick` threw.
- `GET /nav` - our navigator's **interpreted** state (ownership, popup, focus path) that the game-level
  `/focus` cannot see; `[no module]` when the module is not loaded.
- `GET /gui` - **raw** dump of the active uGUI hierarchy (paths, component types, TMP/legacy text,
  CanvasGroup alpha). Surfaces structure `/focus` and `/nav` hide (data living in sub-objects no focus
  label exposes, e.g. DE's Pages/SubPage wrapping); **diff against `/nav`** to find where the mod loses
  information, then `/eval` into what it reveals. Lists active objects only; `/screenshot` is the
  visibility truth for alpha-hidden ones.
- `GET /speech?since=N` - lines the mod has spoken since cursor N (we can't hear the TTS). Response is
  `cursor: <next>` then one line per entry: `<index>: [queue|interrupt] [SpeakingClass] text` - the
  class tag attributes a stray line to its reader. `&wait=MS` long-polls until a new line lands. The
  tap is upstream of the Prism backend, so it works even with speech muted.
- `GET /log?since=N` - the BepInEx log in-band (same cursor protocol; `&grep=S` filters), so nothing
  greps `LogOutput.log` on disk.
- `GET /screenshot` - capture a PNG of the current frame; returns the file path to `Read`.
- `GET /health` - liveness.

**REPL calls exercise Harmony patches.** A proxy method called from `/eval` hits the same detour the
game's own call does, so a patch can be smoke-tested without gameplay: call the patched method, expect
the announcement. If it does not fire from the REPL, the patch is **not applied** - check `/module`,
don't theorize about native-vs-managed call paths.

**Headless / overnight runs:** set `WHIRLING_NO_SPEECH=1` to skip Prism init so an unattended session
doesn't depend on a running screen reader (NVDA). Spoken text is still captured for `/speech`.

Iteration loop for feature code, no game restart: edit `WhirlingInWords.Module`,
`dotnet build src/WhirlingInWords.Module/WhirlingInWords.Module.csproj` (its `DeployModule` target copies just
the unlocked module DLL), then `curl -X POST .../reload` or press **F6** in-game. The host reloads the
module from its DLL bytes; the socket, Prism, and REPL stay live. Host or Core changes still need a
restart (close the game first, since those DLLs are then locked).

The REPL is Roslyn, not Mono.CSharp (whose Reflection.Emit codegen calls net4x AppDomain overloads
the CoreCLR runtime removed, so it throws on every eval). Roslyn's deps (Microsoft.CodeAnalysis.\*,
System.Collections.Immutable/Reflection.Metadata 7.0) deploy beside the plugin and load fine on
BepInEx's CoreCLR.

After a game update, relaunch once through Steam to regenerate `BepInEx/interop/`, and re-dump
`decompiled/` with Cpp2IL.

## Architecture

Four projects, the Hand-of-Fate split adapted to IL2CPP, with the engine-coupled side split again
into a permanent host and a reloadable module so feature code can hot-reload (see Dev driver above):

- **`WhirlingInWords.Core`** (netstandard2.0) - engine-agnostic logic: the speech pipeline, text filter,
  announcement composition, the authored-strings table, and the `IModHost`/`IModModule` contracts.
  References nothing external (no Unity, no BepInEx) so it stays unit-testable off-engine, and it loads
  in the default load context so host and module agree on the contract type identity. If a piece of
  code decides what words the user hears, it belongs here.
- **`WhirlingInWords`** (net6.0) - the permanent host: only what can never reload. The BepInEx `BasePlugin`
  entry, the Prism P/Invoke + backend, the one injected MonoBehaviour pump (`HostPump`; IL2CPP type
  registration is permanent for the process), the dev HTTP server + Mono.CSharp REPL, and the module
  loader. Changing host code needs a game restart, so keep it minimal.
- **`WhirlingInWords.Module`** (net6.0) - the reloadable module: the adapters/readers/announcers and any
  Harmony patches. Implements `IModModule`, driven by the host's pump. Injects **no** IL2CPP types and
  owns no native handles. **This is where day-to-day feature work goes** - it reloads with no restart.
  The host loads it from the DLL bytes into a collectible `AssemblyLoadContext`.
- **`WhirlingInWords.Tests`** (net8.0 + xUnit) - references Core only. No Unity, no game launch.

**Permanent vs reloadable rule.** New feature/adapter/reader/patch code goes in `WhirlingInWords.Module`.
Only entry, native (Prism/P-Invoke), socket-owning, or IL2CPP-type-injecting code goes in the host.
A module must never call `ClassInjector.RegisterTypeInIl2Cpp` (permanent for the process) and must
own no native handle, or it can't be torn down on reload. Module-owned Harmony uses a per-load
`new Harmony(id)` with a per-load UNIQUE id and `UnpatchSelf()` in `Dispose` so a reload cleanly
unpatches: the host loads the new module (which patches) before disposing the old (failed-reload
safety), and `UnpatchSelf` removes by owner id, so a fixed id lets the old teardown strip the fresh
load's patches.

**Adapter / composition split.** Reading live game state touches Unity and lives in a thin adapter in
the module that extracts raw state into plain data (no Unity types past the boundary) and does no
formatting. The announcement is composed from that data by Core, which is unit-tested.

**Announce from the pump.** A hook (Harmony patch, event) records state or sets a dirty flag; the
host pump drives the module's `Tick()` each frame, which reads that and speaks, so announcements
happen once per frame in one place. (Behavioral speech/state/announcement rules are under Conventions
below.)

## Conventions & invariants

**Speech, logging & input**
- All speech goes through `SpeechPipeline` (`WhirlingInWords.Core.Speech.SpeechPipeline`); never call the
  Prism backend directly. All logging goes through the mod's logger (the BepInEx `ManualLogSource`,
  surfaced as `Plugin.Logger`; if logic in Core ever needs to log, route it through a seam so Core
  stays dependency-free), never Unity's `Debug.Log`. Inside any `*.Input` namespace, fully qualify `UnityEngine.Input`.
- Never interrupt existing speech unless an action supersedes it (navigation). Default to queued.

**State & strings**
- **Never cache game state.** Do not copy game data into mod-side dictionaries, lists, or string
  fields for later use; re-query the game when you need a value. A sighted player can see when the
  screen contradicts itself; a blind player trusts speech absolutely, so stale data is worse than no
  data. The only acceptable "cache" is holding a reference to a live Unity component (e.g. a
  `TMP_Text`) and reading its properties at speech time. When several callers read the same game model,
  centralize reads in one View class.
- **Reuse game data, avoid hardcoding.** Use the game's own localized text and live UI state wherever
  possible; look up game strings through I2 Localization (`I2.Loc.LocalizationManager.GetTranslation(term)`,
  or DE's `LocalizationCustomSystem.LocalizationUtils` wrapper), and read dialogue live through the
  Pixel Crushers model (`Subtitle` / `Response`) and orb text via `SenseOrb.GetText()`. Hardcoded text
  goes stale across game updates and blocks translation. Before authoring any string, first check
  whether a game string could be used; only author one when no game data source exists (the mod's own
  screen and section names and status words, like the load announcement, are the usual cases that have
  none).
- **No inline user-facing string literals.** Every word the mod itself authors and speaks must come
  from the mod's central strings table in Core (`WhirlingInWords.Core.Strings.Strings`), never an inline
  literal. Punctuation and log/debug text are exempt. The table is runtime-translatable: each string
  is a key with an English default, and a `lang/<language>.txt` file (loaded by the module's
  `LanguageSync`, which follows the game language) overrides values per key, missing keys falling back
  to English. Word order lives in `{0}`-style templates and plurals in `|`-separated forms picked by
  the file's `_plural` rule - so never concatenate English grammar around a value in code; add a
  template or plural key instead. `lang/en.txt` is the generated translator template; a test pins it
  to `Strings.DumpTemplate()` (on failure, regenerate by writing that string to the file). Names
  rebuilt from English dev data (container flavor names, prop nouns, orb clues) do not translate: in
  a non-English game they fall back to the game's own localized text (a flavor container's single
  item's name) or a generic type word. Spoken game text is un-RTL-fixed (`RtlText.Unfix`): the game
  returns Arabic pre-shaped in visual order for the renderer, which a synthesizer cannot speak - so
  never fetch game strings with fixForRTL on (see `GameLocalization.Translate`/`Term`). The
  whole-line unfix in `TextFilter.Clean` handles only a line that is ONE fixed string: once parts
  are joined, no character reliably marks where one string ends (the game's fixer repositions
  punctuation inside a line, and a short logical word of non-joining letters is byte-identical to a
  fixed fragment). So a composition that joins game text with other text must unfix per part: join
  through `SpokenLine.Join` (which unfixes each part), or `RtlText.Unfix` each game string before
  concatenating. `Strings.F` unfixes its string arguments, so templates are covered.

**Announcements (mod-authored text only — never reword game text).** Users are expert screen-reader
users; strip fluff, never information.
- Distinguishing word first: the sooner the varying part appears, the faster the user moves on
  ("anchored cursor", not "cursor anchored").
- No positional counts ("3 of 10") — the reader tracks position. No nav hints unless an unusual
  control, and on a delay. No redundant context or obvious type suffixes.
- Include all gameplay-relevant detail (dialogue and response text, skill-check odds, item details, an
  orb's type and distance); concise means no fluff, not less information. Avoid emdashes (the reader
  announces them as "dash", breaking flow) and fancy punctuation.
- Selected state uses the word "selected" (`Strings.StatusSelected`), not "active" - the mod standard,
  matching the journal/options tabs (`OptionTab`). Don't invent per-screen status words.
- Read stats/abilities as full words (Intellect, Psyche, Physique, Motorics), never the on-screen
  abbreviations (INT/PSY/FYS/MOT). Full names come from
  `GameLocalization.Translate("Abilities/ABILITY_NAME_" + ability)` where ability is the `AbilityType`
  (the inventory panel object for physique is named "PHQ" but the term is "FYS").
- Label a section/readout for what it is, reusing the game's own header string when one exists (e.g.
  prepend the inventory's "BONUSES FROM ITEMS:" header to the bonuses line).
- Fold an item's detail (effects + description) onto the item itself rather than a separate Tab-stop:
  it saves a keypress and makes specific stats type-ahead searchable. Read detail from the item model,
  not the shared tooltip, which lags a frame behind focus. Don't bind information to dedicated "status
  keys"; make it reachable through normal navigation.

**No silent failures.** The mod runs on a per-frame pump, Harmony patches, and IL2CPP interop, which
fail invisibly unless logged: a swallowed exception in the pump or a patch silently stops a feature
with no error the player can see. Every catch logs via `Plugin.Logger.LogWarning`/`LogError` what
failed and where. No empty catches, no catch-and-return-default without logging. A logged failure is
actionable; a silent one is invisible.

## Changelog

When committing a new feature or bug fix, add an entry to `CHANGELOG.md` under `## Unreleased`, beneath one of two section headers: `New Features and improvements:` or `Bug fixes:`. Add the header if it isn't there yet.

Keep entries terse. One sentence per change, from the player's perspective, ideally under ~120 characters. State the change directly and stop.

Player-facing means player language.

Do not include:

- Pre-fix behavior beyond what the fix itself implies. "The character sheet no longer speaks stale skill totals after equipping an item" is the entry; "It used to read the old bonus, but now re-reads the live value" is bloat.
- Multiple quoted strings illustrating the same point. At most one short parenthetical example, and only if the entry is ambiguous without it.
- Meta-commentary about parity with sighted players, rationale, or how a sighted player would experience the fix. The reader knows the audience.
- Implementation detail, file paths, internal symbol names.
- Bug fixes for unreleased features.

## Gotchas

Recurring traps found while building readers. These bite again on each new screen, so check them
before assuming a control reads cleanly.

**Animated numbers read 4x and out of order.** DE renders numbers with `NumericFlipClock` (and
subclasses `AbilityGradeFlipClock`/`MoneyFlipClock`/`LongNumericFlipClock`), a split-flap that stacks
the digit as several `TMP_Text` layers, so a `GetComponentsInChildren<TMP_Text>` sweep returns the
same digit several times, often before its label ("5 5 5 5 INT"). Read `flipClock.targetValue` (an int,
exposed as a property by Il2CppInterop; use `targetValue` not `currentValue` so it's right mid-animation).
Hits archetype stats, character-sheet attributes/skills, and money.

**Menu labels are not all TMP, and captions are display-styled.** Many menus (save/load especially)
are mostly legacy `UnityEngine.UI.Text`, not TMP, so sweep both. Button captions are bracket-framed
ALL-CAPS for display ("[ DELETE SELECTED]") while the I2 term resolves to natural case ("Delete
selected"); strip the brackets (UI-scoped only, not in the general TextFilter) so it recases and isn't
voiced as "left bracket". Icon buttons are image-only with no text child: their root `I2.Loc.Localize`
term ends in `_IMG`; the spoken caption lives at the sibling `Buttons/<base>_TEXT` term.

**Tooling traps.** Launch via `steam.exe -applaunch 632470` (the `steam://rungameid/...` URL silently
no-ops here). Kill the game with `MSYS_NO_PATHCONV=1 taskkill.exe /F /IM disco.exe` (plain `taskkill`
from the Bash tool mangles `/F` into a path). Don't invoke `powershell.exe` from the Bash tool; the
auto-mode classifier blocks it, so build with `dotnet build` directly. Adding or altering a
`WhirlingInWords.Core` type is NOT picked up by F6/`POST /reload` (Core loads permanently in the host's
default context); the reload reports success but `Module.Tick` then throws `TypeLoadException` every
frame, so do a full restart (close game, `dotnet build`, relaunch). Pure-module edits hot-reload fine.

## Common LLM Antipatterns

### Comments and docs: state what is, not what isn't
Comments and documentation describe the current state and why - not the change history, the absence of
something, or a path not taken. Consider whether a comment is needed at all.

**WRONG**: `// Removed the old UI system. Now x does y.`
**WRONG**: `// Changed to use controllers. Now handles force_close`
**WRONG**: `// We don't use the Prismatoid NuGet` (documents a non-thing)
**CORRECT**: `// Can be closed with the controller`

This governs descriptive text. Prescriptive rules and API contracts ("never call the backend
directly") and a what-happens fact that justifies an instruction ("launch through Steam; running
disco.exe directly exits") state what to do and are fine.

### Defensive null handling
Excessive validation hides bugs. Only null-check where null is a legitimate, expected state (e.g.,
after `FirstOrDefault()`, at public API boundaries). Let code crash otherwise — a crash is visible, a
silently swallowed null is not. Trust private callers.

**WRONG** — silently returning empty instead of crashing:
```csharp
if (entity == null) return new List();
var controller = entity.GetControlBehavior();
if (controller == null) return new List();
```

**WRONG** — `?.` on things that should never be null:
`var name = entity?.GetController()?.Sections?.FirstOrDefault()?.Name ?? "default";`

**CORRECT**: `var name = entity.GetController().Sections.FirstOrDefault()?.Name ?? "default";`

### No throwaway dev hacks
Never hack a temporary bypass into the tree to dodge a proper reload or restart (e.g.
`if (_devEnabled || true)` to force-enable a dev affordance). The tree stays clean at all times - the
hot-reload (F6 / `POST /reload`) and rebuild+relaunch loops are cheap and meant to be used normally. To
toggle a gated dev feature, set its real config (env var, etc.) and reload or relaunch. Keep gates
honest.
