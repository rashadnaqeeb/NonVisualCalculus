# World Navigation: What Was Built

The infrastructure for world navigation, ported from the WOTR accessibility mod and adapted to
DiscoAccess's architecture. This is the build record and decision log; the feasibility findings that
preceded it are in `world-navigation-scouting.md`. Two leaf features now sit on this foundation, the
**wall tones** and the **scanner** (the review cursor); the sonar is not yet built (see Deferred, below).

Everything here was validated live in Martinaise via the dev server, and the implementation was audited
line-by-line against the WOTR source; the divergences recorded below are deliberate.

## What it does today

A blind player can move a freeform cursor around the isometric scene with the live world keys and hear
its position relative to the character ("northeast, 2 meters"), with the cursor clamped to walkable
ground so it cannot leave the floor; press Enter to walk the character to the thing under the cursor and
interact (a conversation, a container, an exit) or to bare ground; and reach the game's information
screens, the pause and help menus, the status readouts (time, money, health), and the gameplay
quick-actions through the world keymap. The navigation model below is wired: being in the world owns the
keyboard the same way a migrated menu does. **Wall tones** sound the nearest walls in the four cardinals
around the cursor, so the player hears the room's edges as they glide. The **cursor object cue** names the
things it passes over: a stereo click as the cursor crosses each thing's real footprint while gliding, and
the thing's spoken name (resolved by `EntityNaming` from the game's own authored name) folded into the point
readout when the glide stops. The cursor senses exactly the actionable interactable set, the same set the
Enter verb acts on, through one shared selection, so what it names and what Enter clicks can never disagree.
The **scanner** (PageUp/PageDown, see its section below) browses the same actionable set as a review
cursor: cycle it by category, hear each thing's name, bearing, distance, and a stereo ping, then plant
the cursor on it (Home) or walk to it and act (I) - a tactical overview that never moves your position.
Underneath, a live registry classifies every entity in the area and filters it down to that set (with
the game's own fog of war hiding unentered rooms), and an audio engine places stereo cues. What is
still missing to make the world fully legible is the last leaf sensing system, the sonar, which turns
the registry into an ambient sense of what surrounds the cursor.

## Architecture

The world layer follows the project's existing four-project split, with the engine-coupled side divided
into a permanent host and a reloadable module.

- `DiscoAccess.Core` (no Unity reference) holds the engine-agnostic logic: the spatial math, the
  overlay framework, the taxonomy, the audio-engine contract, and the world-model contracts. Because
  Core cannot reference Unity, the world point type is `System.Numerics.Vector3` (XZ ground plane, Y up,
  one unit equals one meter), and the Module converts from Unity's `Vector3` at the boundary. This is
  what keeps the sensing logic unit-testable off-engine.
- `DiscoAccess.Module` (reloadable) holds the engine-touching adapters: the environment seam, the world
  registry and its proxies, and the world reader that drives the overlay. This is where the live game
  reads happen.
- `DiscoAccess` (permanent host) holds the NAudio engine, because the audio device is a native handle
  and must live beside Prism where it never reloads. It is lent to the module through `IModHost.Audio`,
  the same way speech and settings are lent.

The dependency shape is two foundations (the overlay framework and the world-model data layer) with the
sensing systems as leaves hanging off them. The framework and the data layer are independent of each
other; both must exist before any sensing system does.

## The pieces, by layer

Core spatial foundation (`Core/World/`):

- `Geo` computes straight-line (3D) distance, the eight-point compass bearing (planar, and reported as
  "no bearing" when a thing is directly above or below), the above/below vertical sign, and
  here-detection. It returns raw values; turning them into words is the announce layer's job, so Geo
  stays free of the string table.
- `Spatial` holds the sonar and wall-tone placement formulas (pan, distance volume, the quadratic
  proximity curve, and the sweep-gap pacing), ported from WOTR with distances in meters.
- `ScanBounds` is the shape geometry: a point, a circle footprint, a doorway's disjoint segments, and a
  connected polyline, each able to report its nearest point to a reference (so distance and bearing
  measure to the near edge of a wide thing, not its center).
- `SpatialReadout` composes the spoken cursor line (bearing first when there is one, then meters, then
  above/below) from authored strings.
- `WorldMath` is the shared clamp helper (netstandard2.0 has no `Math.Clamp`).

Core overlay framework (`Core/World/Overlays/`):

- `OverlaySystem` is a pure sensing provider: it queries the world relative to the cursor and either
  contributes a spoken announcement or makes sound each frame. It never moves the cursor and never reads
  input. Its play mode (Off / WhenMoving / Continuous) is read live through a bound provider.
- `Overlay` is the one container: it holds systems (one per concrete type), glides the cursor (only
  while the player has control), tracks motion, and runs the announce pipeline that joins every system's
  contribution into one spoken line.
- `Cursor` is the freeform-glide point of attention, navmesh-clamped through the environment seam.
- `IWorldEnvironment` is the engine seam the framework reads (player position, control state, the navmesh
  glide-clamp, and `WallDistance`, a cardinal navmesh cast for the wall tones). `MotionTracker`,
  `OverlayContext`, `OverlayAnnouncement`, `PlayMode`, and `AnnouncementContext` round it out.
- `SpatialSystem` (under `Overlays/Systems/`) is the cursor point readout (bearing, distance, height).
- `WallToneSystem` (under `Overlays/Systems/`) is the first audio leaf: each frame it casts the navmesh in
  the four cardinals from the cursor and turns each distance-to-wall into a 0..1 volume on the
  `Spatial.ProximityVolume` curve, then drives the engine's four wall-tone voices. It mutes (zeroing the
  volumes, keeping the voices) when the play gate is closed, control is lost, or a menu owns input over the
  world (`Overlay.InputActive`). Its volume and play mode are read live through bound providers.
- `ObjectCueSystem` (under `Overlays/Systems/`) is the cursor's sense of the things it glides over, the
  WOTR `ObjectCueSystem` model: while gliding it plays a short stereo blip each time the cursor crosses
  into or out of a thing's footprint (a rising click on entering one, including swapping straight from one
  thing to another; a falling click on leaving to bare ground, panned toward the thing); and on a glide
  stroke ending it contributes the name of the thing under the cursor to the point readout, so the player
  hears "crate; southeast, 11 meters" (name first, then the spatial system's position). The blip, the spoken
  name, and the Enter verb all call one selection, `ObjectCueSystem.Under` (the nearest actionable
  interactable whose real footprint the cursor is within a small margin of, skipping the player's own
  entity), so they can never name one thing and act on another. The set is the accessible non-orb
  interactables, the exact set Enter can act on - scenery and the not-yet-interactable orbs are neither
  named nor clicked. It self-gates like the wall tones (silent under a cutscene, lost control, or a menu
  over the world).

Core audio contract (`Core/Audio/`):

- `IAudioEngine` and `IWallTones` are the engine seam in plain floats: a panned one-shot and the four
  directional wall-tone voices. Sensing systems compute pan and volume themselves and hand the placed
  sound here.

Core world-model contracts (`Core/World/`):

- `IWorldItem` is the sensing-facing view of a thing (name, position, bounds, category, IsAccessible,
  IsVisible, Interact), implemented by a Module proxy that reads live. `Bounds` is a real footprint: a `Box`
  sized to the entity's combined solid-mesh renderer bounds (measured once, since size is structural, with
  the centre read live), so the cursor is "on" a thing anywhere over its surface, not only dead-centre.
- `IWorldModel` is the registry contract: the live collection of items plus `Added`/`Removed` events, so a
  consumer can attach to a thing (for example a sonar voice) and follow it rather than re-scanning.
- `WorldTaxonomy` is the flat category set (npc, door, exit, container, orb, other).
- `EntityNaming` resolves the spoken name from the raw fields a proxy extracts (engine-free, unit-tested).
  It prefers the game's own authored name, which the proxy resolves per type: for a character or prop the
  examine conversation's CONVERSANT actor, localized ("Yard Cuno" reads "Cuno", "Eternite_door" reads "Pile
  of Eternite"); for an exit the DESTINATION it leads to plus the portal type read from the GameObject.name
  (door / gate / stairs / elevator, else "exit"), so "Whirling in Rags door", "floor 2 stairs", "Tent exit".
  An exit's destination is its distinct localized area name when that differs from where you are (another
  building, or a floor the game names, "Bookstore"); when the destination shares the current name (all
  Whirling floors are "Whirling-in-Rags") it falls to the floor/level from the scene id suffix, so an
  inter-floor staircase reads "floor 2 stairs" (or "basement stairs" for a "-s<n>" sublevel). A door onto the
  main exterior whose own name is a specific spot reads that spot, defaulting to "door" ("balcony door"), so
  it is not hidden behind the coarse "Martinaise". Hyphens in an authored name are spoken as spaces
  ("Whirling-in-Rags" to "Whirling in Rags"), and a "Name, the Title" actor name keeps just the name before
  the comma ("Garte, the Cafeteria Manager" to "Garte"). Failing an authored name it falls back to
  the object noun from the `GameObject.name` (the last word of "Harbor Crate 22" is "crate"; "box_3 rooftop"
  carries its noun before the underscore, "box"), and last to a spoiler-filtered examine title for the
  location-slug form ("Ice_eternite" to "Eternite"). Unidentified NPCs are safe because DE's actor name IS
  the display name (unknown characters are literally named "Working Class Woman"), read live so it reflects
  the game's current reveal state. Validated live across Martinaise.

Host audio (`DiscoAccess/Audio/`):

- `NAudioEngine` is one shared mixer feeding one output device. The wall tones loop WOTR's set-1 WAV
  assets (north/south/east/west, decoded once and cached, summed at fixed compass pan: east hard right,
  west hard left, north/south centred); the procedural one-shot is still a windowed sine with constant-
  power pan. `PlayCue(AudioCue, volume, pan)` fires a sampled one-shot the engine owns (a decoded mono WAV
  panned constant-power, auto-removed when finished) for the cursor enter/exit clicks: `CursorEnter` is the
  rising click, `CursorExit` the falling one. The device opens lazily and self-disables on failure, so a
  machine with no audio device never crashes the mod. The wall-tone WAVs deploy beside the plugin under
  `assets/audio/walltones/1`, the cursor cue WAVs (`enter.wav`/`exit.wav`) under `assets/audio/cursor`.

Module world layer (`Module/World/`):

- `WorldEnvironment` implements `IWorldEnvironment` over the live game: player position from
  `Party.Player.Main`'s transform, control state, and a navmesh-clamped trace via `NavMesh.Raycast` plus
  `SamplePosition`.
- `WorldModel` is the poll-and-diff registry over `BasicEntity.sceneEntitySet` and the active
  `SenseOrb`s, keeping one stable proxy per game object, throttled to about 10 Hz.
- `EntityProxy` and `OrbProxy` are the live `IWorldItem` proxies. Entities classify by the game's
  `Interactable` subclass tree via `TryCast`; orbs read `GetText` and `orbType`. `WorldConvert`
  centralizes the Unity to System.Numerics conversion.
- `WorldReader` owns the one overlay, engages it on entering the world and disengages on leaving, and
  ticks both the registry and the overlay each frame. It constructs the `WallToneSystem` (binding its play
  mode to the continuous-wall-tones setting and its volume to the wall-tone-volume setting) and sets
  `Overlay.InputActive` each frame so the tones mute when a menu owns input over the world. It is wired
  into `UiModule`.

The world settings (`Core/Settings/`): wall tones are the first world system with a settings UI. A numeric
`RangeSetting` (a 0..100 percent with a step, alongside the existing `ToggleSetting`, both under a shared
`ModSetting` base the menu lists in order) backs the **wall tone volume** (default 5%, deliberately low so
the tones sit under speech), and a `ToggleSetting` backs **continuous wall tones** (default off: tones play
only while the cursor glides plus the motion linger; on: always while in the world). The settings store
gained int persistence, and `ModMenuScreen` renders each setting by type (`SettingToggleCell` /
`SettingRangeCell`).

## Key decisions

The simplified overlay model. WOTR lets the user cycle between named overlay presets. We dropped that:
Disco has one implicit overlay whose systems are toggled from the mod settings menu. The cycling was a
preset layer on top of the per-system toggles, not the toggles themselves, so dropping it also removes
WOTR's per-overlay settings-inheritance machinery. The three-way play mode (Off / WhenMoving /
Continuous) stays in the model even though the menu may present a plain on/off, so "sonar only while
gliding" can be surfaced later without rework.

No movement-mode abstraction. WOTR supports a tile-stepper and a freeform glider on one cursor. Disco
keeps only the freeform glide, so the `MovementMode` layer is gone and the cursor owns the one glide
directly.

Movement intent counts even when blocked. Holding the cursor keys registers as moving even when the
cursor cannot advance (against a wall) and even without control, matching WOTR, so a WhenMoving system
stays smooth instead of stuttering. An audio system that must fall silent during a cutscene gates on
control itself rather than on this motion signal (a requirement noted for the wall-tone and sonar
systems when they are built).

Core uses `System.Numerics.Vector3`, not Unity's. This is forced by Core having no Unity reference, and
it matches the project's "no Unity types past the boundary" rule. It needed the `System.Numerics.Vectors`
package, which is not in the netstandard2.0 surface by default.

The spoken distance is straight-line 3D, not planar. The city has real elevation within a single map
(only the Whirling's floors are separate maps reached by scene transitions), so a thing up on a ledge
reads as genuinely farther rather than only being tagged "above". The compass bearing stays planar (a
compass has no vertical component, and the readout omits it entirely when a thing is directly above or
below), and the above/below tag gives the vertical direction. This diverges from WOTR, which used the
game's rules-distance metric (half-weighting vertical gaps) that Disco has no equivalent for.

The in-world gate is `ViewType.CLEAR`. This is the non-obvious one. `ScreenAdapter` maps `ViewType.LOBBY`
to the world screen name and lists `CLEAR` among "silenced transition states", which suggests gating on
LOBBY. Verified live, the opposite is true: free-roam gameplay reads `CLEAR` steadily with the full
entity set present, while `LOBBY` reads false in-world. The code-review pass flagged this as a bug
(reasoning from the static mapping without running the game); it was rejected with the live evidence,
and the gate comment records why so it is not re-flagged. `HasControl` (a character exists and no
conversation is active) gates the finer cutscene/dialogue case on top.

Audio is NAudio via NuGet, host-side. The scouting notes established we must compute pan and volume
ourselves and stay off the game's mixer, so the cues are not colored by its DSP. NAudio is the WOTR-proven
choice and runs on BepInEx's CoreCLR. The wall tones use WOTR's authored set-1 WAVs (the user's choice, to
keep the same feel); the sonar one-shot is still a generated tone and can move to sampled audio when
per-category assets are authored.

Wall tones port WOTR's math, in metres. The proximity-volume curve (`(1 - dist/range)^2`, biting close in)
and the 0.25s motion linger were already ported with the framework; the system adds the cardinal navmesh
cast and the voice driving. The sense range is WOTR's 10 ft converted exactly to 3.048 m, so the curve
bites at the same physical distance and the soundscape feels identical. The play mode maps onto the
existing three-way `PlayMode`: continuous-on is `Continuous`, continuous-off is `WhenMoving` (the motion
linger is the "x ms after stop"). The default volume is 5% (an ambient bed under speech, not a foreground
cue), diverging from WOTR's 100% on purpose. Muting is gated on three things, not just lost control: the
play gate, `HasControl` (cutscene/conversation), and `Overlay.InputActive` (a menu floating over the
in-world view, where the game still reads CLEAR but the overlay is no longer driven) — the last closes the
gap where continuous tones would otherwise drone under the mod menu.

A flat taxonomy, at two granularities. WOTR has a two-level category tree. Disco's smaller world gets a
flat set, but the set exists at two grains: the fine classification a proxy reports
(npc/door/exit/container/orb/interactable - `interactable` is the conversational-prop remainder, the
trash can with lines behind the Whirling), which the naming rules key off (an in-place door names by its
examine actor, a transition exit by its destination), and the browse categories the scanner cycles and
the sonar will sonify (npc/interactable/container/orb/exit), where **door folds into exit** via
`WorldTaxonomy.ScanCategory`. The fold is the player model, confirmed against the game's type tree: a
`Door` never changes area (it swings open in place, reconnecting the navmesh - the Whirling bedroom
doors), the area-changers are all `TransitionEntity`, and to the player both are "a way through", the
WOTR rule that a closed door cutting the navmesh IS the exit there.

`IsAccessible` is the actionability gate. The registry holds everything (about 420 entities in
Martinaise), and `IsAccessible` collapses it to the roughly 100 actionable things a sighted player with
this build could act on, filtering the container clutter (368 containers down to about 62). The cursor
sees the full set; the sonar and scanner will read the accessible set.

The registry polls at about 10 Hz, not every frame. The per-frame cost is dominated by
`FindObjectsOfType`, a full scene scan, and membership need not be frame-fresh: the proxies read their
own live state on demand, and the consumers act on the order of seconds. This was a code-review fix.

Proxies read live, never cache. Per the project's "never cache game state" rule, every proxy property
reads the live game object on access. The only thing held is the proxy identity (so the scanner can keep
a selection), keyed on the Unity object via the interop wrapper cache.

## The seams

The boundaries between Core and the engine are three interfaces, each implemented in the Module or host:

- `IWorldEnvironment` (Module) gives the overlay framework the player position, control state, and
  navmesh clamp.
- `IAudioEngine` / `IWallTones` (host) give the sensing systems a place to play already-spatialized
  sound.
- `IWorldItem` / `IWorldModel` (Module) give the sensing systems the live entity data.

This is what lets the sensing logic live in Core and be unit-tested with fakes, while the live reads stay
in the Module.

## Dev hooks

`WorldReader` exposes a set of `Dev*` methods (and a static `Active` reference) so the dev server can
drive and inspect the cursor, the audio engine, and the registry. With the keyboard model now wired,
these are introspection and headless-validation scaffolding (a backgrounded window receives no OS keys,
so the live keys cannot be exercised through the dev server), not a player feature.

## The navigation model

The keyboard-ownership and interaction model was the deferred fork above the infrastructure; it is now
built and wired, and validated live in the Whirling backyard (glide to Yard Cuno and Enter starts his
conversation; the walled-off Yard Woodpile reads unreachable; the screen, status, and quick-action keys
all fire through the real input registry). It lives in three Module pieces: `WorldReader` promoted to the
world's keyboard owner, `WalkInteract` (the Enter state machine), and `WorldCommands` (the game-acting
hotkeys: screens, pause/help, status reads, quick-actions). The cursor verbs and the held glide are
read in `UiModule` alongside the menu input, under a new `InputCategory.World` that is live only while
the world owns the keyboard.

### Keyboard ownership: the world is the largest migrated screen

Being in the isometric world is owning the keyboard, the same way a migrated menu screen owns it. There
is no toggle mode to enter or leave. Whenever `ViewsPagesBridge.Current` reads `CLEAR`, `WorldReader`
takes the same one lever `ScreenManager` uses for menus, `InControl.InputManager.Enabled = false`,
reasserted each frame, and restores it on leaving the world. That lever is all-or-nothing: it mutes
every game key at once (scouting confirmed targeted mutes do not take), so the model is to mute the game
wholesale and re-provide each key we want. Our own keys poll `UnityEngine.Input` directly, below
InControl, so they keep working while the game's are dead; our actions call the game's APIs directly
(`Character.SetDestination`, `entity.Interact`), never through input, the same pattern the menu navigator
uses for `NavigationManager.Select`. A blind player gets nothing from the live camera or click
underneath, so there is no vanilla play to preserve, which is what makes the wholesale mute the right
choice over fragile per-key blocking.

The cost of muting everything is that every game action we still want needs its own binding. That is the
keymap below.

### The keymap

Cursor and interaction:
- W A S D glide the cursor on the ground plane (W north, S south, A west, D east), navmesh-clamped.
- C recenters the cursor on the character.
- Enter walks the character to the cursor's target and interacts (the walk-then-interact verb below); on
  bare ground it just walks there.
- Space cancels the current walk and stops movement (the game's `StopMovement`), so a committed Enter is
  abortable mid-path.

Information screens, the game's own hotkey letter plus Ctrl: Ctrl+I inventory, Ctrl+C character sheet,
Ctrl+J journal, Ctrl+T thought cabinet. Each opens the game's own view by invoking its HUD menu button's
click, and our screen reader then drives it; Escape (the screen's Back) closes it. Escape opens the pause
menu and F1 opens help, both through the game's own `ViewController.ToggleView`. There is no map key: the
map has no standalone view (it is a sub-page inside the journal, reachable via Ctrl+J), so a dedicated key
would need fragile cross-frame sub-page navigation for no access the journal does not already give. The
mod settings menu stays on Ctrl+M (the planned F12 move was only to free Ctrl+M for the dropped map key).

Gameplay quick-actions with no screen of their own:
- Left and Right arrow use a healing charge on the Health and Morale bars (matching the controller dpad's
  left and right), via the game's healing pools; refused, with feedback, when there is no charge or the
  bar is already full, so a charge is never wasted.
- 1 and 2 use the left-hand and right-hand equipped items (`InventoryLuaFunctions.UseSubstanceInHand`); an
  empty hand reads as such.
- F5 and F8 quicksave and quickload through the game's persistence API (quickload refused when no
  quicksave exists).
- Ctrl+L cycles language, global, in the world and in menus (the game's bare-key binding was killed by
  type-ahead, so it is restored here under Ctrl), then speaks the new language's own name.

Status:
- T reads the time (the game's own day-and-hour string), M reads money, H reads the two bars Health and
  Morale, each current of maximum with its count of healing charges. Each press re-reads. Plain keys,
  distinct by modifier from Ctrl+T (thought cabinet). The bars are named by the game's Health/Morale terms,
  not the Endurance/Volition skills that set their maximums.

The scanner (the review cursor), WOTR's key scheme:
- PageDown and PageUp cycle the reviewed thing through the current category, nearest-first from the
  movement cursor; Ctrl+PageDown and Ctrl+PageUp step the browse category (Everything, people,
  interactables, containers, orbs, exits), skipping empty ones.
- Home plants the movement cursor on the reviewed thing (the explicit bridge from reviewing to being
  there; the camera follows, so it is also "go look at it").
- I walks the character to the reviewed thing and interacts, through the same walk-then-interact verb
  as Enter (bare I; Ctrl+I is the inventory, distinct by modifier).

Still unassigned in the world: Up and Down arrow, Q, and Tab (freed when the scanner took the WOTR
keys instead of the Tab reservation).

Talk-to-Kim is intentionally dropped: you reach the same conversation by walking to Kim and interacting,
so it needs no dedicated key.

### Enter: walk-then-interact

`Interact()` does not walk the character. Confirmed live: calling it on a container 11 meters off
returned false and the character did not move; it interacts in place and refuses when out of range,
playing a fail animation. The walk-then-interact fusion lives in the game's click handler, not in
`Interact()`, so Enter orchestrates it itself, with every piece proven live:

1. Target the entity's `GetInteractionLocation(currentLocation)` stand-point, never its raw
   `transform.position`. Many entities sit off the navmesh, doors embedded in walls, props up on ledges,
   so the raw position is unreachable; the stand-point is the game's designated on-navmesh approach spot.
2. `SetDestination` to the stand-point with `MovementMode.AUTOMATIC` (see Movement speed), watching
   `movementStatus` for arrival.
3. `Interact(new Interactable.ClickEventData())` once `IsWithinInteractionRadius` is true.

Both ends are confirmed live. Out of range, `Interact` returns false and the character stays put (the
woodpile at 11 meters). On arrival at a reachable target's stand-point it returns true and the
interaction begins: walking Harry to Yard Cuno's stand-point, `Interact` returned true once inside the
radius and a real conversation started (`isConversationActive`). Conversations do not change the view,
`ViewsPagesBridge.Current` stays `CLEAR` while one runs, which is why the world layer gates on
`HasControl` (no active conversation) on top of the `CLEAR` view.

Orbs and physical objects share this one path. An orb's clickable (`OrbUiElement`) only activates once
the character is inside the `SenseOrb`'s `InteractionRadius`, but the `SenseOrb` is already in the
registry at its world position while out of range, so the cursor can navigate to it before it is
clickable. Confirmed live: `IsOrbiting` flips exactly at the `InteractionRadius` (false at 3.5 meters,
true at 3.0 for a radius-3 orb), and crossing the radius only makes the orb appear, it does not auto-fire
the conversation. The clickable `OrbUI` carries a second gate, the orb must be rendered (the camera on
it); a distance-only approach with the camera elsewhere leaves the orb in range (`IsOrbiting` true) but
its `OrbUI` still inactive. In normal play the two coincide because the camera follows the character,
which is why camera-follow (Deferred) is required for orb interaction, not only for streaming. On bare
ground with no target, Enter is just `SetDestination`.

### Reachability: attempt the game's own interaction, don't pre-judge it

Enter does not pre-reject on the reachability oracle `CheckIfCanCreatePathToHavePath`. That oracle tests
whether the character can path to the interaction stand-point, but it reports false for interactables the
game can still act on by walking the final leg itself: Garte the bartender carries a real conversation,
yet his stand-point sits behind the bar on a navmesh pocket the player cannot path to, so the oracle says
unreachable while the game's own `Interact()` walks the character around and starts the dialogue (proven
live). So gating Enter on the oracle wrongly refused Garte. Instead, Enter walks toward the target and
calls the game's `Interact()` on arrival, and again if the walk stalls near it (an NPC behind a counter);
only when `Interact()` itself refuses is the thing genuinely unreachable, and then Enter says so. Do not
roll our own `NavMesh.CalculatePath` to the entity's body either: an NPC's feet can sit on an off-mesh
sliver, a false negative (this falsely reported the talkable Cunoesse unreachable during testing).

The oracle stays on the proxy as `IsActionable(from)` for the future sonar and scanner (a cheap
per-position "could I act on this" signal), but it is no longer the Enter gate. `IsAccessible` is not a
reachability guarantee either: the Yard Woodpile reads `IsAccessible = true` yet is walled off from the
backyard navmesh, so its walk stalls and `Interact()` refuses, and Enter reports "can't reach" after the
attempt rather than pre-judging. A thing unreachable from here can become reachable once the character
has moved.

### Talk-across-barrier, and position versus actionability

The stand-point sits on the player's side of a barrier, so a fenced NPC is talkable without being
reachable, and Enter handles it with no special-casing. Cunoesse's body is 5.6 meters away behind a
fence (a `PathPartial`, unreachable), but her interaction stand-point is 3.9 meters on Harry's side (a
`PathComplete`); you walk to the stand-point and converse across the fence, exactly as a sighted player
does. So an entity's spoken position and its actionability are two independent facts: position describes
where the body is, for the player's spatial map; actionability is the oracle's verdict on the
stand-point. They can disagree, Cunoesse reads far and behind a fence yet is fully actionable, the
woodpile reads closer yet is not.

### The interaction point is what the player-facing tools represent

Every actionable interactable is represented to the player by its interaction point
(`GetInteractionLocation`), not its body: the sonar pings it, the scanner targets it, and the spoken
go-here distance and bearing measure to it. That is the point the player navigates to in order to act,
it sits on reachable ground, and it dissolves the barrier case (Cunoesse pings at her reachable
talk-spot, not her unreachable body), so following any cue leads to a successful interaction. The point
is approach-relative, recomputed from the querying position, so it is a live navigation target rather
than a fixed landmark; in practice it holds still as you walk straight at a thing and only shifts if a
better approach side opens. Computing it is heavier than reading a transform, so it is done for the
sonar's current sweep set and on scanner navigation, throttled, not for the whole accessible set every
frame.

The body position is kept for two narrow uses: the free-glide exploration cursor's spatial readout (what
is physically around you, the look-around sense), and locating a genuinely-unreachable thing to announce
it as behind a wall. So the proxy exposes three facts per item: `Position` (the body), `InteractionPoint`,
and `IsActionable` (the oracle's verdict). The actionable tools key off the interaction point; the
exploration cursor keys off the body.

### Movement speed

`SetDestination` takes a `MovementMode` (`AUTOMATIC`, `RUN`, `WALK`, `INSTANT`, `TELEPORT`). Enter passes
`AUTOMATIC`, which behaves identically to a vanilla click: the game's own policy decides walk versus run,
honoring the player's run preference and any scripted spot where the game wants careful movement. We
never hardcode `RUN`. There is no stamina cost to running, but some scripted moments reward moving
slowly, so forcing run would both break parity and risk overriding those; `AUTOMATIC` keeps us safe by
deferring to the game. An always-run preference, if wanted, is exposed through the game's own setting
mirrored in the mod menu, not forced per path, so it too stays subject to the game's contextual
overrides.

### What this changed in the code

`WorldReader` was promoted from a dev-driven cursor host to a real keyboard owner: `ResolveOwnership`
runs each frame before input is polled and, while the view is `CLEAR` with control and no menu screen
taking it, takes the InControl lever (yielding to the screen manager, which is authoritative), and the
held glide vector and the cursor verbs are read in `UiModule` under `InputCategory.World`, live only
while the world owns the keyboard. The lever is restored on leaving, never fighting the screen manager's.
The `Dev*` hooks remain only as introspection. The proxies (`EntityProxy`, `OrbProxy`) gained
`InteractionPoint(from)` and `IsActionable(from)`, both approach-relative and reading live through
`GetInteractionLocation` and `CheckIfCanCreatePathToHavePath` (an orb has no stand-point, so it reports
its body and is not actionable). The Enter verb is `WalkInteract`, a small arrival-watching state machine
(target the stand-point, drive `SetDestination` with `AUTOMATIC`, watch `movementStatus`, call the game's
`Interact` on arrival - and again if the walk stalls near the target, letting the game walk the final leg
into a spot our path could not reach), cancellable by Space. The game-acting hotkeys (screens, pause/help, status reads,
quick-actions) live in `WorldCommands`, each calling the game's own method directly since the wholesale
mute leaves no key for the game to read.

## The scanner (review cursor)

The second leaf feature, WOTR's scanner model ported whole: a categorized, distance-sorted browse of
the actionable things in the area whose selection is a **second point of attention** alongside the
movement cursor - the look-without-moving counterpart (NVDA object-navigator style). Cycling it speaks
the thing's name and its bearing and distance and pings it in stereo, without moving the cursor, the
camera, or the character; the two act verbs (Home, I) are the explicit bridges from reviewing to acting.
This is deliberately NOT a mover of the one cursor: WOTR runs two cursors with two act verbs (Enter acts
on what the movement cursor is inside; I acts on the review selection; Home connects them), and that
model ports unchanged, so the cursor's "name and Enter can never disagree" invariant is never touched.

The mechanics, all WOTR's: the list is rebuilt from the live registry on every keypress (the world set
changes as rooms reveal and orbs stream; a held list would go stale), the selection is continued across
rebuilds by proxy identity, the first press announces without stepping, a fresh list enters at the
nearest thing (or farthest, stepping backward), empty categories are skipped (the synthetic Everything
at index 0 always lands), and a vanished selection re-enters at nearest. Divergences: no per-item
positional counts ("3 of 12" violates the announcement rules; the category landing does speak the set
size, "exits, 6", which is information), and no subcategory level (the taxonomy is flat). The spoken
distance and bearing measure to the thing's `InteractionPoint` (computed only for the landed thing; the
sort uses cheap body positions), and the review ping is spatialized to the same point - `Spatial.Pan` /
`DistanceVolume`, the cursor-enter cue until per-category sounds are authored with the sonar. The set
is `IsAccessible && IsVisible`, the same gate the cursor's own sense uses. `Core/World/Scanner.cs` is
the engine-free logic (unit-tested); `WorldReader` owns it and exposes the verbs; the keys live in
`UiModule` under `InputCategory.World`.

## Fog of war: the IsVisible gate

Building the scanner surfaced a real fog-of-war system the scouting pass missed ("Disco has no fog" is
true only outdoors). Interiors hide unentered rooms behind `Sunshine.Unseen.Zone` volumes (parented
under "fow-unrevealers"), rendered as black void, with a three-state status - UNSEEN (never entered),
ACTIVE (on view now), INACTIVE (seen before, dimmed) - the exact knowable/currently-seen split WOTR's
visibility model uses. The game's own `MouseOverHighlight.RegisterAndTurnMeOff` finds an entity's zone
by raycasting straight up on the fog layer (mask 0x2000); `EntityProxy.IsVisible` replicates that probe,
hiding only UNSEEN, with no zone (exteriors, revealed space) visible. The probe distance is 4 m: the
co-loaded Whirling floors stack about 6 m apart in world space, so a longer cast would hit the floor
above's volume and hide things in plain view.

The body probe alone over-hides **boundary doors**: a closed room's own door sits in the wall under the
room's volume, yet is plainly seen (and knocked on) from the corridor. A fogged body therefore gets a
second probe at its interaction stand-point, which sits on the approach side: stand-point in revealed
space = visible boundary (Klaasje's corridor door), stand-point also fogged = genuinely inside (her
bathroom door, reachable only through the room). Validated live on Whirling floor 2 with her room
UNSEEN. This gate composes into everything downstream: the cursor's `Under()` (so it never names what a
sighted player cannot see), the Enter verb, and the scanner's lists and counts - the chained-door case
(reviewing a door inside an unrevealed room) simply cannot arise, because the door is not offered until
its room reveals, one step at a time, exactly the sighted experience. Known residual: a fog volume that
does not quite cover a room's far door (Kim's bathroom door on floor 2) leaks that door as visible - the
game's own accessibility data leaks it identically, and acting on it reports can't-reach.

## Camera follow

The exploration cursor drives the game camera so the orbs around wherever the player is looking stream in
(orbs are camera-frustum culled by `NPCUnloader`, so an orb only wakes, gaining its text and type, once the
camera is on it), and so an orb under the cursor is rendered, the gate its clickable needs before it can be
interacted with. Validated live in the Whirling backyard: gliding the cursor moved the camera focus to
track it, the orb set changed as it moved, and the camera held on a resting cursor instead of snapping back
to the character.

The mechanism is a small testable `CameraFollow` (`Core/World/Overlays/`) behind a new `IWorldEnvironment`
seam (`FocusCamera`/`ReleaseCamera`), driven from `Overlay.Tick` and gated on `InputActive && HasControl`.
It re-focuses only after the cursor drifts past two metres (one camera move per couple of metres, not per
frame; a re-focus to an unchanged point would needlessly recompute the frustum and churn the streamer), and
releases on going inactive or on overlay exit. The Module backs `FocusCamera` with
`CameraController.Current.SetFocus(point, no-zoom, instant: true)`.

The non-obvious decision is the camera lock. The game has no automatic character-follow in free roam, but
it *does* reclaim the camera back to the character in the gaps when we stop issuing `SetFocus` (confirmed
live: a resting cursor's camera drifted back to the character at the character-focus height within a
second). So `FocusCamera` first takes a camera lock (`AddLock`, with a stable per-environment
`Il2CppSystem.Object` token, re-added via `CheckLock` if the controller was swapped on an area change),
which freezes the game's own camera logic; `SetFocus` still drives the camera while the lock is held
(confirmed live, contrary to a first reading of the decompile). `isSlaved` is left untouched, since
toggling it stalls orb streaming. On release we remove only our own token (never
`RemoveAllLocksAndCenterViewport`, which would drop the game's locks too), and the game then recenters on
the character. During a conversation the game adds its own camera lock for dialogue framing; our lock has
already released by then (control is lost), so the two never fight, confirmed by the full round trip: glide
to Yard Cuno, Enter, his conversation starts with our lock gone and the game's in place, and on the
conversation ending the world re-engages and the cursor follows again.

## Deferred

These are real forks left open on purpose, not oversights.

- The held-glide and physical-keypress path is exercised in the design's mechanism but not through real
  OS keys in the headless dev harness: a backgrounded window receives no OS key events, and the dev
  server drives the menu navigator, not the world's raw-key poll. The cursor verbs and every game-acting
  handler were validated by firing their registered `InputAction` directly through the live `InputManager`
  (and the glide through the overlay), which exercises the same code a key would; the one unverified link
  is `Input.GetKey`/`GetKeyDown` itself returning for a held W or a pressed Enter, which uses the same
  poll the working menu keys do. Worth a sighted confirmation pass on a focused window.
- The dedicated map key. The map has no standalone view (it is a journal sub-page), so a Ctrl+M map key
  would need cross-frame sub-page navigation; dropped, since the map is reachable through Ctrl+J's journal.
- The remaining leaf sensing system: the sonar sweep. (Wall tones and the scanner are now built.) The
  sonar must suppress out-of-sight things (those behind a wall from the cursor), or it is annoying; the
  gate is a navmesh line-of-sight raycast from the cursor to the thing's nearest point, added to the
  environment seam when the sonar is built - the fog IsVisible gate is necessary but not sufficient for
  it (fog hides unentered rooms; the navmesh LOS also suppresses the revealed-but-walled-off). Note the
  navmesh LOS must never gate the scanner or cursor naming: it would hide Cunoesse behind her fence,
  who a sighted player sees and talks to across it.
- Scanner follow-ups deferred on purpose: orbs list awake-only (a dormant camera-streamed orb has no
  text or type to speak; the camera-follow streams them in as you look around, and a measured wake takes
  0.3 to 1.2 seconds after the camera lands, so a camera-nudge-on-review enhancement is cheap if the
  small category proves annoying); per-category review-ping sounds arrive with the sonar's audio pass.
- The remaining settings-menu wiring for the world systems (the sonar's on/off and "what does the sonar
  sonify" category toggles). The wall tones' volume and continuous/when-moving toggle are wired.
- Sampled audio assets, if the procedural cues prove insufficient.
- Bounds refinement: doorway segments. Proxies now report a real `Box` footprint from renderer bounds
  (`ScanBounds.Box`), so the point-bound stand-in is gone; the disjoint-segment shape for a doorway's portal
  edges still exists in `ScanBounds` unused, if a door ever wants its opening measured rather than its box.
- Area / district announcement is now built (`DistrictReader` in the module), modelled on wotr-access's
  room-transition tracker. The game has no runtime sub-districts (it names whole scenes only), so the mod
  authors the partition as labelled anchor points and classifies the reference by the nearest one - a Voronoi
  tiling, no overlaps or gaps. Two readouts: an auto-announce that speaks the sub-district the instant the
  cursor-else-player reference crosses into a new one, and the 'r' key that reads the full location, the
  game's localized map name plus the sub-district ("Martinaise, Harbour"; "Whirling in Rags floor 1"
  indoors). Elevated micro-districts (the Whirling balcony, the Capeside roof and balcony) are separated from
  the ground beneath by folding height into the distance. District names live in `Strings` (settled); the
  anchor coordinates live in the module so they hot-reload. The earlier name-prefix idea was dropped: the
  designer name prefixes proved coarse and un-localizable (the container groups are organizational, "West"
  sits in the east, "Jam" is the traffic jam), so authored anchors plus the game's own map localization win.
  Remaining: the anchor coordinates are a rough first pass tuned live during a full playthrough, and interior
  room subdivision (a kitchen / backroom split, via the wotr navmesh watershed) is not yet authored.
- Orb interaction. Camera follow (now built, see above) keeps an orb under the cursor rendered, which is
  the gate its clickable needs, so this is unblocked: the remaining work is making `OrbProxy.IsActionable`
  and `Interact` real (likely through `SenseOrb.StartConversation`, in range) and letting the Enter verb
  target orbs, which the cursor's shared `Under` selection currently skips (orbs are excluded by category).
- Recentering the cursor onto the player when an area is entered.

## Validation snapshot

Loaded into the Whirling-in-Rags backyard: the registry classified about 420 entities (368 containers
matching the scouting probe, 9 NPCs, 6 exits, about 21 orbs), and `IsAccessible` filtered them to about
104 actionable. The cursor glided navmesh-clamped (stopping at a fence about 4 meters out, not running
to infinity) and recenter snapped it back to the character. Readouts spoke correct bearings and
distances. The audio device opened and played panned one-shots and wall tones with no error.

With the wall-tone leaf since built, a later live pass in the same area confirmed the mod loads clean, the
set-1 WAVs decode at the deployed path, and the volume and continuous settings read their defaults and
persist; the system drives its voices each frame with no pump exceptions. Actual audibility and the
held-glide-while-moving OS-key path still want a sighted/hearing confirmation on a focused window. 205 unit
tests pass.
