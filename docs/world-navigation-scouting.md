# World Navigation: Scouting Notes

Feasibility findings for porting WOTR-style world navigation into NonVisualCalculus. Everything
here was confirmed against the running game (Martinaise exterior, opening) via the dev `/eval`
REPL unless marked otherwise. This is a scouting reference, not a build plan.

## Goal

Port the world-navigation systems from the WOTR accessibility mod (`C:\Users\rasha\Documents\wotr-access`):
a freeform exploration cursor, a scanner of interactables and orbs, a sonar to sonify them, and
wall tones. Drop what Disco doesn't need (combat, party, formations, ability targeting, world-map
travel) and drop tile-mode cursor; keep only the freeform cursor. We do not write our own pathing;
we ride the game's.

## How portable WOTR is

WOTR's exploration code cleanly separates engine-agnostic logic from engine glue.

- Reuse (lands in `NonVisualCalculus.Core`): the overlay framework (an `OverlayManager` holding overlays,
  each with one shared cursor plus a list of `OverlaySystem` lenses, movement modes kept separate
  from systems), the scanner taxonomy and shape geometry, spatial math, the sonar sweep timing and
  pan/volume formulas, and the announcement composer.
- Rebind per game (thin Module adapter): entity enumeration, navmesh queries, visibility, and
  interaction routing.

WOTR's whole design polls a persistent entity registry each frame into stable proxies. That pattern
holds in Disco.

## WOTR mechanics to reuse

These are the concrete formulas behind the four pillars (from the WOTR source), to port directly with
distances in meters:

- Wall tones: per frame, cast a navmesh ray in each of the 4 cardinal directions; volume per direction
  is `(1 - distance/range)^2` (quadratic, bites close in); range default ~3m. Pan is fixed compass
  (E/W = +/-1, N/S = 0) when feeding a stereo mixer.
- Sonar sweep: snapshot the visible interactables within a radius (default ~12m) that have a sound,
  sorted left to right by X for a smooth pan glide. Ping one at a time; gap between pings
  `= clamp(0.75/count, 0.1, 0.2)s`; rest ~0.4s between sweeps (nothing dropped, busy areas just take
  longer).
- Sonar spatialization per ping, to the nearest point of the item's shape: pan
  `= clamp(dx / max(dist, panWidth), -1, 1)` with panWidth ~3m; volume `= clamp(refDist/(refDist+dist),
  0.08, 1)` with refDist ~3m. Inside the shape: pan 0, volume 1.
- The fog/visibility slot: WOTR keeps everything in the world model and gates per item with
  `IsVisible` (knowable) and `CurrentlySeen` (visible now). In Disco that slot is filled by
  `IsAccessible` plus the camera-streaming of orbs.

## World scale

One world unit equals one meter (standard Unity). Confirmed: nav agent height 1.8, radius 0.25,
walk speed 1.14 m/s, jog 3.5 m/s. No scale constant. Unlike WOTR (which used 0.3048 to convert
meters to feet for D&D distances), Disco has no distance fiction, so announce in meters directly,
factor 1.0. Set every distance tunable (cursor speed, sonar pan width, wall-tone range) in meters.
A sensible cursor glide default is about 4 to 5 m/s (WOTR's 15 ft/s).

## World model access points

The entity layer is NOT camera-culled: all ~400 entities stay active across the whole ~200m
district regardless of camera, with valid positions. So the object scanner reads the entire loaded
area at once, no caching.

- Orbs: `SenseOrb`, enumerated via `FindObjectsOfType<SenseOrb>`. The `sceneOrbSet` registry is a
  transient work queue and `usedOrbs` is a recycle pool, so neither is the source. Content is
  `GetText()` (full localized prose). `morselText` exists but is almost always empty.
- Entities: `FortressOccident.BasicEntity.sceneEntitySet` (static list of all world objects: NPCs,
  containers, doors, items, props). Valid `transform.position` and `name`.
- Player: `FortressOccident.Party.Player.Main` (a `Character`).
- NPC presence is schedule-driven (time and story), which is correct behavior.

## Navmesh

Disco uses Unity's `NavMeshAgent` through `FortressOccident.CharacterNavigator`. The stock
`UnityEngine.AI.NavMesh` (in the `UnityEngine.AIModule` interop proxy) is baked and queryable:

- `NavMesh.SamplePosition` snaps a point onto the mesh (cursor clamp).
- `NavMesh.Raycast` returns the first walkable-to-blocked boundary (wall tones). Cardinal raycasts
  from the player returned real wall distances.

Wall tones and cursor clamping port from WOTR by swapping its A* calls for these. Note: the dev REPL
cannot compile against `AIModule` (use reflection there); real Module code references the proxy
normally.

## Camera and streaming

The map is co-loaded but not all active. 33 scenes load in memory, but orb content is camera-frustum
gated by `NPCUnloader`: only ~35 orbs are active with text at any moment; ~707 are streamed out as
positional ghosts (position valid, but `orbType` NONE and empty text). Verified by panning the camera
88m to a dormant cluster and watching orbs wake with full text, then restoring.

Implication: orbs reveal as you look, like fog lifting, which matches sighted play. The natural design
is to have the movement cursor drive the camera (`CameraController.Current`) so orbs stream in around
it. Pan via the game's `SetFocus` / lock API; do NOT toggle `isSlaved` (it stalls orb streaming and is
disruptive).

## Naming

There is no single clean display-name field. `MouseOverHighlight` is outline-only (no text). Objects
are not parented to orbs (1 of 402). `conversantActorName` is set on only 5 of 402 and names the
speaker, not the object.

- `GameObject.name` is primary and mostly clean (~85%), with a sluggy tail (door slugs like
  `courtyard-door-crypto-garys-apt`, `Eternite_door`).
- The `conversation` title is a cautious fallback only, NEVER a default. It frequently leaks spoilers
  and meta: titles like `FOOTPRINTS VISCAL`, `STONE PERC`, and branch conditions like
  `red thread outside if clicked on thread inside`. Using it by default would tell a blind player
  things a sighted player cannot see.
- Rule: clean `GameObject.name` first; fall back to a sanitized conversation title only when the name
  is a slug, the entity is not a named character, and the title passes a spoiler filter (strip the
  `AREA /` and `ORB` prefixes; reject mechanical or conditional tokens such as PERC, CHECK, a
  difficulty number, "if", "earlier"/"later", multiple clauses); otherwise use a generic type word
  plus the spatial readout. Duplicates (three "can") are disambiguated by position.

## Taxonomy

Comes from the game's `Interactable` subclass tree, read via `TryCast` (NOT `GetType`, which the
interop boxes to `BasicEntity`):

- `FortressOccident.Character` = NPC.
- `FortressOccident.Door` (and `KimDoor`, `TequilaDoor`, `Curtains`) = doors.
- `Sunshine.ContainerSource` = containers.
- `Sunshine.MoneyItem` = money. NOTE: money piles in the world (`Harbor Wall Money`, `Jam Money`) are
  actually `ContainerSource` holding a "money" item, not `MoneyItem` instances; `MoneyItem` was absent
  in this area. So "money" is a container-contents detail, not usually its own type.
- `FortressOccident.TransitionEntity` / `TravelDestination` / `Teleporter` = exits.
  `NavMeshClickHandler` is the ground/click handler in the same tree (`TravelDestination` derives from it).
- `OrbUiElement` = orb-UI.

There is also a coarse `INTERACTABLE_TYPES` enum (NONE/NPC/ITEM/THOUGHT) and `InteractableType`
(ORB/MOUSE_HIGHLIGHT, the `CommonPadInteractable` wrapper kind). In Martinaise the split was roughly
10 NPCs, 8 exits, 368 containers, 17 orb-UI. The 368-container bucket is the noise (Disco models all
clutter as lootable containers).

`MouseOverHighlight` presence is NOT a usable interactable-vs-scenery discriminator: ~386 of 404
entities carry one (it is the outline shader, on nearly everything). The real filter is `IsAccessible`,
below.

## Actionability filter (the noise solution)

`BasicEntity.IsAccessible` is the gate that separates signal from litter. (`CanInteract` is true for
everything, so it is not the filter.) `IsAccessible` folds together three things, all computed against
the real character and read live:

1. Equipment/condition gating. Cans and empty bottles read non-accessible without the collection bag;
   real containers (crates, dumpsters, money) read accessible.
2. Passive skill checks. Disco hides loot behind passive Perception checks scaling by difficulty
   (very easy up to legendary). `IsCheckPassed` reflects whether THIS character passes; failed checks
   are non-accessible and hidden. Example: Jam Window (legendary) and Ice Money (challenging) read
   `IsCheckPassed=false`, `IsAccessible=false`, hidden; Yard Woodpile (very easy) passes and is
   accessible.
3. Physical reachability. Items that pass the check but are not yet reachable (e.g. Ice Pillars out on
   the ice) read `IsCheckPassed=true` but `IsAccessible=false`.

Filtering on `IsAccessible` collapses ~400 entities to ~91 actionable ones and is parity-correct: it
reveals exactly what a sighted player with this build can see. It matches the set the game lights up
for Tab-highlight and detective mode (`MouseOverHighlight.UpdateHighlights`). Because it reads live,
equipping the bag or raising Perception and re-scanning just works (no caching).

## Container detail (`Sunshine.ContainerSource`)

Reads cleanly before opening:

- Contents: `Count`, `containedItems`, `GetItem(i)`. Returns internal item term IDs
  (`jacket_navalcoat`, `hat_bum`, `nosaphed`, `money`) that need resolving to localized names.
- Lock/tool: `isLocked`, `openedWithCrowbar` (e.g. Pier Dumpster: locked, crowbar, holds a naval coat).
- Skill gate: `skillType` / `InteractableSkill` plus `difficulty` (enum: EXTRATRIVIAL, TRIVIAL, ... up).
- Flags: `ContainsVisibleItems`, `IsCheckPassed`, `PrerequisitesMet`, raw `conditionString`.

Parity guard for contents: announce items only when accessible (and the sighted player would see the
icons). A perception-hidden container disguised as scenery (`Landsend Rock`, `Flower Pot`) may have its
mundane name spoken but never its hidden contents.

## Interaction and move-to (proven end to end)

- Move-to: `Character.SetDestination(point, Il2CppSystem.Nullable<float> heading, MovementMode, bool force)`.
  Call `Character.EnableCharacterMovement()` first; pass `Character.movementMode` and `force=true`.
  Fired it; character walked to a sampled navmesh point (status IDLE to MOVING to arrived). The
  low-level `CharacterNavigator.SetDestination` alone does NOT drive movement; use the `Character` one.
  Track progress/arrival via `Character.movementStatus` (IDLE/MOVING) or `CharacterNavigator.hasPath`
  / `pathEndPosition` / `isOnNavMesh`.
- Interaction: `entity.Interact(new Interactable.ClickEventData())`. Fired it on the footprints clue;
  it started that conversation a frame later. Returns true when accepted, false when a conversation is
  already active.
- Dialogue state: `PixelCrushers.DialogueSystem.DialogueManager.isConversationActive` /
  `lastConversationStarted` / `StopConversation` (detect and clear dialogue).

## The three-tool model

The game's own mechanisms map onto our three tools:

- Cursor: the full entity set. Announces everything visible it passes over (scenery included), so the
  name-cleaning layer matters even for clutter. "Visible" means what a sighted player can see, so
  perception-hidden contents stay gated on `IsAccessible`.
- Scanner and sonar: the `IsAccessible` set only (~91 actionable things), sorted and pinged by category.
- Right-stick snap (`CommonPadInteractable`): the game's nearby reachable-interactable list,
  proximity-sorted, with `CanInteract()` and `PathToInteractableExist()`. Good as a nearest-interactable
  helper, but proximity-limited, so not the whole-area scanner source.

## Audio

Use an external engine like WOTR's NAudio, NOT Unity audio (which routes through the game's mixer and
DSP and would color the cues). Compute pan and volume ourselves. By the host/module split, the audio
device handle lives in the permanent host alongside Prism; the module decides what to play.

## Probing gotchas

- Il2CppInterop boxes registry elements to the declared list type, so `GetType()` returns
  `BasicEntity`; classify with `TryCast<T>()`.
- The dev REPL has a fixed assembly reference set: `UnityEngine.AIModule`, `PixelCrushers`, and the
  Physics module are not referenced, so reach those types via reflection in the REPL (real Module code
  references the proxies directly).
- `Il2CppSystem.Nullable<float>`, not `System.Nullable`, for the `SetDestination` heading.
- Static interactable registries: `SenseOrb.sceneOrbSet` (transient), `NPCUnloader.usedOrbs` /
  `unusedOrbs` (pools), `MouseOverHighlight.registry` (~386). For active orbs use
  `FindObjectsOfType<SenseOrb>`.

## Open items for build time (none block feasibility)

- The "where am I" area-name source. `Sunshine.AreaManager` holds scene-name constants
  (`MARTINAISE_EXT`, `NVC_INT_F1/F2`, etc.) and an `additivelyLoadedScenes` list, but
  `AreaManager.current` read null. The current area is identifiable as the loaded non-interior scene
  name (e.g. `Martinaise-ext`); a localized display title still needs locating.
- Resolve item term IDs to localized names for container contents.
- Inline markup in orb `GetText` (double-dashes, asterisks) for the text filter.
- How the Whirling floors stack for multi-level cursor movement (elevation).
- Wiring the cursor to the camera so orbs reveal correctly as you explore.
