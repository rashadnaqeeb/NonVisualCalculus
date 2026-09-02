# Environmental Descriptions: Plan

How Non-Visual Calculus will put the painted world of Disco Elysium into words: geometric rooms
derived from the navmesh, and three channels of mod-authored prose (room ambiance, object
appearance, painted details) anchored to the world and spoken through the existing world reader.
This is the build plan agreed on 2026-09-02. It follows the model of the WOTR accessibility mod's
environmental-description feature, adapted where Disco differs.

The WOTR source is available for reference at `C:\Users\rashadnaqeeb\Documents\wotr-access`
(synced with origin/main on 2026-09-02). The files that matter:

- `src/Exploration/RoomMap.cs`: the navmesh watershed segmentation, exits, and curation loader.
- `src/Exploration/EnvDescriptions.cs`: the anchor store and the describe readouts.
- `src/Exploration/AreaDetails.cs`: curated detail points as scan items.
- `src/Events/RoomChangedEvent.cs`: the room-change announcement.
- `src/Dev/DevSurvey.cs` and `tools/survey.py`: the capture and survey tooling.
- `tools/rooms_render.py`: offline floor-plan rendering of a room dump, for tuning.
- `docs/design/environmental-descriptions.md` and `docs/design/description-rules.md`: the design
  background and the authoring rules. The rules file is the base for ours.
- `assets/descriptions/*.json` and `assets/locale/enGB/ui.json`: real authored data to model on.

## Who authors

Claude (the assistant working in this repo) writes every description. The pipeline is designed
around that: captures and data land in files, the prose is written from those files, and the user
reviews the result by listening in the live game. The user is the reviewer and the arbiter of
register and detail, not the author. Nothing in this plan expects a person to write prose.

## Decisions already made

- Outdoor space is handled by the same room segmentation as interiors. In WOTR the burning city of
  Kenabres is 112 authored regions: streets segment at narrowings, squares stay whole, and curated
  virtual walls split a big open area where it honestly holds several places. Martinaise is the same
  kind of space.
- No unexplored state anywhere in this feature. Disco's fog is not a reliable marker of what a
  sighted player has seen, so the WOTR unexplored percentage and the spot-level unexplored flag are
  dropped. The fog volumes are used, if at all, only as an interior partition seed.
- No asset dedupe. In WOTR the object table is keyed by prefab name so one entry covers hundreds of
  instances. Disco's world is uniquely drawn: nearly every interactable is its own painting. The
  unit of authoring is the entity, keyed by scene and GameObject name, one entry each.
- Object prose describes the look, never the meaning. The game's examine conversation and the orbs
  say what a thing is and what Harry thinks; they very rarely describe appearance, because a sighted
  player already sees it. Our text supplies exactly that gap and never restates the game's writing.
- Strict separation from game text. Mod prose is spoken only by its own keys and its own
  announcements. It is never appended to an orb, an examine line, or any other game text, so a
  sentence heard as the game's is always the game's.
- Player-facing disclosure of authorship is deferred. Not part of this pass.

## The three prose channels

1. Room ambiance. One authored title and body per room (more only for a genuinely distinct zone
   inside one room), anchored to a world coordinate. The title is a short noun phrase that doubles as
   the room's spoken name ("The Whirling's Kitchen"); the body is two to five sentences on the
   permanent stage: geometry, floors, furniture, light, decals, wreckage, atmosphere.
2. Object appearance. One entry per interactable entity, keyed by scene plus GameObject name. Spoken
   for the focused scanner item. Describes size, material, colour, condition, what it sits on or
   against. Never its state (open, locked, empty), never its contents, never what it means.
3. Painted details. Points with no entity behind them: a mural, graffiti, a wreck, a shanty wall,
   anything that only exists as scene art. Each is a coordinate plus prose, surfaced as a scan item
   in its own browse category so it cycles, sonifies, carries bearing and distance, and can be
   walked to like anything else. In WOTR this was a niche fix for one puzzle. In Disco it is central,
   because most of what a sighted player sees is backdrop with no data.

## Architecture

### Room map

- A Core algorithm, `NonVisualCalculus.Core.World.Rooms` (name provisional), ported from WOTR's
  RoomMap as pure grid math over an abstract walkability raster: per-cell walkable flag and surface
  height. Distance transform for clearance, persistence watershed to split basins at clearance dips,
  small-region merge, stable ids sorted by centroid, class words by area and elongation, height
  gating between cells, curation walls and merges. Unit-tested in `NonVisualCalculus.Tests` on
  synthetic floor plans.
- A module adapter fills the raster. The Unity navmesh triangulation API is stripped from the
  game's interop (confirmed: `CalculateTriangulation` is absent from the AIModule proxy;
  `SamplePosition`, `Raycast`, and `FindClosestEdge` are present), so the adapter samples
  `NavMesh.SamplePosition` at cell centres over the scene's bounds. Built once per scene on a
  retry cooldown, since the mesh may not be ready the frame the scene name changes.
- Disco's interiors and floors are separate scenes (the mod already speaks a floor word from the
  scene name), so height gating matters less than in WOTR. Interiors also carry the game's own
  per-room fog volumes (`Sunshine.Unseen.Zone`), which the mod already probes for visibility.
  Whether to gate cells by zone so the game's hand-made room volumes override the heuristic indoors
  is decided after seeing the un-seeded result on the Whirling.
- Thresholds (cell size, persistence, minimum room area, furniture island size) get their own
  tuning pass on Disco maps. WOTR's were chosen by eye on WOTR maps. A DEBUG dump of the grid plus
  a port of `rooms_render.py` lets us look at a floor plan offline before tuning live.
- Exits between rooms come from cells on a room boundary whose neighbour is another room, clustered
  into openings. Whether openings become scan items in the exits category (the WOTR "next room exit"
  cycle) is decided in phase 2; the immediate use is naming: a door or opening speaks the room it
  leads into.

### Description store

- Per scene, two files under `assets/descriptions/`:
  - `<scene>.json`: geometry and anchors only, no prose. `walls` and `merges` for curation,
    `rooms` as `{x, y, z, key}` anchors, `details` as `{x, y, z, key}` points, `objects` as a list
    of entity GameObject names that have text. Coordinates in the game's world frame.
  - `<scene>.<lang>.txt`: the prose, in the same key = value format as `lang/<language>.txt`, so
    the existing parser and translator workflow are reused. Keys: `room.<key>.title`,
    `room.<key>.body`, `detail.<key>`, `object.<name>`. English is the fallback for a missing key
    in another language, matching LanguageSync.
- Anchors are world coordinates, never room ids. The room map is a live membership test: the room
  description spoken at the cursor is the anchor that resolves to the same room the cursor resolves
  to. Segmentation can change under the data without breaking it.
- Loaded by a module `DescriptionStore` on scene change and on F6, following the game language.
  This is mod data, not game state, so holding it in memory is fine.

### Surfacing

- Describe object: speaks the focused scanner item's entry, else "nothing described". The focused
  item is already filtered by the game's own accessibility, so gated objects cannot leak.
- Describe room: speaks the title and body of the room the cursor stands in, else "nothing
  described".
- Details: a new `detail` entry in `WorldTaxonomy`, folded into the Items quick-nav group and
  listed in the scan cycle and the sonar toggles (both enumerate the taxonomy, so the settings menu
  picks it up). A detail speaks its prose as its name, or a short title as the name and the prose on
  the describe key; decided after hearing both.
- Location readout (R) and the automatic arrival line gain the room after the area name: the
  authored title if there is one, else "Room N" plus its class word.
- Room-change announcement from the world tick: when the cursor (else the player) resolves to a
  different room, speak the same room line, queued. A settings toggle, on by default, mirroring
  WOTR's announce-rooms setting.
- Keys are a decision for the user. Free world keys today include V, E, F, G, K, N, O, Q, U, Y, and
  Z. Proposal: V for describe object, Shift+V for describe room. X stays read-experience.
- All spoken framing words (the nothing-described line, the room class words, the "Room N"
  template) go in the central strings table. The prose itself lives in the description files.

## Collecting the objects

The game gates objects with data we can read while they are gated, so enumeration does not depend
on reaching any game state (read from the Ghidra decompile of `BasicEntity`):

- `IsAccessible` is `IsCheckPassed` and `PrerequisitesMet`.
- `PrerequisitesMet` looks up the entity's conversation "Condition" field, a Lua expression, and
  runs it live. No condition means always met.
- `IsCheckPassed` is the perception-style reveal from the conversation's CheckType and Difficulty
  fields.
- Story-toggled objects are inactive GameObjects until a script enables them.

The survey therefore:

- Enumerates the full loaded scene with the include-inactive object search, not the mod's live
  world model, which only sees registered active entities.
- Records per entity: scene, GameObject name, position, category, active state, hidden flag,
  accessible flag, the raw Condition string, check type and difficulty, and the conversation title.
  The Condition string is the author's note on when the thing exists; the author never needs to
  satisfy it.
- Captures gated or inactive objects by enabling them and their renderers for the shot, then
  restoring, with nothing written to the save. This departs from WOTR's author-from-the-live-game
  rule and is safe here because the prose is pure appearance and is only spoken when the game has
  made the object focusable.
- Treats a story variant as its own entry. Disco nearly always swaps GameObjects for a changed
  look, so each variant has its own name and its own text. A single object whose look changes by
  animation state is noted in the survey data and handled case by case.
- Is validated, not collected, across saves: a diff of the survey list between a day 1, a day 3,
  and a late save catches anything instantiated at runtime. Spawned characters are out of scope.

## Authoring pipeline

1. Survey. A DEBUG-only `DevSurvey` class in the module, called through `/eval`, plus a Python
   driver against the dev server, ported from WOTR's. Room mode: per room, farthest-point survey
   spots plus the centroid, a screenshot at each, and a `data.json` with the room's entities split
   into people and things, plus the anchor candidates. Object mode: one framed shot per entity,
   with the gate data above. Detail mode: manual coordinates chosen from the room shots, one framed
   shot each. Captures hide the HUD, force the fog volumes visible, and restore camera and fog after.
   Screenshots are downscaled for review.
2. Author. Claude reads each room folder and object shot and writes the prose files. Rooms first
   for a scene, then objects, then details picked while looking at the room shots. Every claim needs
   a close capture of that exact spot; nothing is inferred from the corner of a wide shot or invented
   to fill a gap.
3. Validate offline. Every anchor and detail key has prose in every shipped language, every object
   name in the JSON exists in the survey list, no orphan prose keys, no duplicate keys.
4. Validate live. Every anchor resolves to a room, every room in a curated scene has a title (an
   untitled room next to titled ones is a curation bug), every object key matches a live entity.
5. Review. The user walks the scene and listens. Register, length, and detail are tuned on the
   first scene and then applied everywhere.

Rules for the prose (the WOTR `description-rules.md` adapted; the full file is written in phase 3):

- Permanent stage only: geometry, furniture, floors, props, decals, light sources, weather-fixed
  atmosphere. Nothing that appears or disappears: people, anything with lifecycle state, loot,
  anything Harry can change.
- Appearance, not meaning. What it looks like, not what it is for or what it signifies. The game
  says the rest.
- No compass directions or distances in prose. The mod speaks bearing and distance live from the
  anchor; prose uses relative placement only ("against the far wall", "under the window").
- Nothing tied to time of day or weather that changes. Disco has a day cycle; captures are taken at
  one time and the prose must not describe the light of that moment as permanent.
- Geometry seen past a wall cutaway is not in the room. The isometric camera slices walls open; a
  stair or door glimpsed in a neighbouring room must be checked against the room graph before prose
  claims it, and if it is out of reach, it says so.
- Unflinching. A mature game; describe what is there, plainly.
- Titles are short noun phrases that work as a place name in an exit readout and a location line.

## Phases

1. Room map. Core algorithm with tests, module raster adapter, DEBUG grid dump and offline renderer.
   Tune on the Whirling interior and the Martinaise exterior until the floor plans look right to
   the user's ear when walked. Deliverable: rooms resolve at the cursor in both scenes.
2. Room surfacing. Room in the location readout and arrival line, room-change announcement with its
   toggle, doors and exits named by destination room, and the decision on openings as exit items.
   Deliverable: walking Martinaise names the places as you enter them.
3. Description store and keys. File formats, loader, describe-object and describe-room keys, the
   detail category, the strings-table entries, offline validator, the adapted rules file.
   Deliverable: a hand-written test entry in each channel speaks in the live game.
4. Survey tooling. `DevSurvey` and the Python driver: room, object, and detail modes, gated-object
   capture, the cross-save diff. Deliverable: a complete survey of the Whirling ground floor and the
   plaza.
5. Content pass. Author the Whirling and the plaza first, review with the user, fix the register,
   then proceed scene by scene through Martinaise, the coast, and the interiors. Each scene lands
   as its own commit with its survey diffed against at least two saves.

## Verified live (2026-09-02, Doomed Commercial Area, second floor)

Checked through the dev server with the game running. These fix the details above.

- Enumeration: `Resources.FindObjectsOfTypeAll<BasicEntity>` works through the interop and must be
  filtered by `gameObject.scene`, since it also returns prefabs and assets (2699 objects, of which
  20 belonged to the scene, 12 of them inactive). Story variants are present as inactive twins
  ("Safety Curtains" beside the active "Safety Curtains Opened", the back door beside its "broken"
  variant), confirming one entry per variant.
- Gates: `IsAccessible`, `PrerequisitesMet`, `IsCheckPassed`, and `InteractableSkillThreshold` read
  on gated and inactive entities. The conversation's Condition, CheckType, and Difficulty fields read
  through `Field.LookupValue(entity.ConversationObject.fields, name)`. `isHidden` has no getter
  through the proxy; `isHiddenCached` is the readable field.
- Raster: scene bounds come from the scene's renderers after dropping the handful whose bounds are
  thousands of metres tall (backdrop planes). For this floor that gave a 37 by 64 m box, and a
  0.25 m `SamplePosition` sweep over it took 204 ms for 38 656 cells (1972 walkable, 123 square
  metres). A large exterior will be several times that, still well under a few seconds once per
  scene. The sample must also reject hits that snapped more than a cell away, or off-mesh cells read
  walkable.
- Fog volumes: `FindObjectsOfType<Sunshine.Unseen.Zone>` returns the scene's volumes under the
  "fow-revealers" object, each named for its room (gym, tower, stairs, main, storage, back), with a
  collider whose bounds give the room's footprint. `status` is writable: forcing a volume to ACTIVE
  and writing the old value back restores it. The names alone are a strong hint for room titles and
  an interior partition seed.
- Camera: the camera follows the player while `CameraController.Current.IsSlaved` is true and
  ignores `SetFocus` in that state. Setting `IsSlaved` false, then `SetFocus(worldPoint)` and
  `Zoom` (orthographic size; 3 gave a tight object frame) moves the camera on the next frame.
  Restoring is setting the saved focus and zoom and `IsSlaved` back to true, which snaps to the
  player. `SetSpecialSlave` is the inventory and thought-cabinet path and drags the player in; not
  for this.
- Screenshot: the dev server's screenshot is full resolution (3456 by 2168 on this machine) and is
  written a frame after the request. Captured with the camera on the gym barbell, it framed the
  spot correctly with the HUD still overlaid in the corners.
- Axes: the camera is orthographic at yaw 180, so world +x is screen left and world +z is screen
  down. The mod's compass already encodes this: Core north is Unity minus z (screen up) and Core
  east is Unity minus x (screen right), through `WorldConvert`. Every survey shot is annotated
  with that frame; no direction is ever derived from a picture.

Still open, none blocking:

- A HUD-hide path for cleaner captures. Leads: the game's `GetScreenShot` and the collage mode.
  Disabling the HUD canvas roots for the frame is the fallback.
- Interactable counts per scene, to size the content pass. Needs each scene loaded; collect as the
  survey visits them.

## Risks

- Interior over-segmentation. Disco's rooms are small and crowded; the furniture-island threshold
  and the minimum room area will need Disco values, and some scenes will need merges.
- Wide open ground under-segmentation on the coast and the plaza, handled by curated walls.
- Content volume. Object descriptions are one per entity across every scene. The survey's
  incremental mode and per-scene commits keep this tractable, but it is the long pole.
- Drift with game updates is low: the game is patch-frozen, and anchors are coordinates.
