# Zones and Environmental Descriptions: Plan

How Non-Visual Calculus will divide Disco Elysium's walkable world into named zones and put the
painted world into words. Written 2026-10-13 from a fresh read of the WOTR accessibility mod
(`~/Documents/access-mods/wotr-access`, synced with origin/main the same day) and of this mod's
world layer, then checked against the live game the same day (Part 9). It replaces nothing: the
earlier plan in this folder was discarded unread.

Two decisions taken by the user that shape everything below: zone names are authored, every zone
in every scene, because Disco's map is small enough to name completely; and there is no
unexplored state anywhere.

The WOTR files this plan draws on, for anyone checking the source:

- `src/Exploration/RoomMap.cs`: the navmesh watershed that makes the zones, exits, curation.
- `src/Exploration/EnvDescriptions.cs`: room anchors and the describe readouts.
- `src/Exploration/AreaDetails.cs`: curated detail points as scan items.
- `src/Dev/DevSurvey.cs`, `tools/survey.py`, `tools/rooms_render.py`: the capture tooling.
- `docs/design/environmental-descriptions.md`, `docs/design/description-rules.md`: design and
  the authoring checklist.
- `assets/descriptions/*.json` plus the `desc.*` keys in `assets/locale/enGB/ui.json`: the
  shipped data (twenty areas, 490 room anchors, the largest being Kenabres at 124).

## Part 1. How WOTR did it

### How zones were picked

Zones are not authored. They are computed from the walkable surface each time an area loads, and
the authored data only names them. The pipeline, in order:

1. Rasterize the navmesh (the recast triangles) onto a 0.25 m grid: one walkable flag and one
   surface height per cell. The cell coarsens if the grid would pass 1.7 million cells.
2. Clearance: a distance transform gives every cell its distance to the nearest wall. Interior
   obstacle islands up to 12 square metres (furniture, pillars) cast no clearance shadow, so a
   table in the middle of a room does not split the room.
3. Persistence watershed: cells are visited in order of descending clearance and merged into
   basins. Two basins stay separate only where they meet across a dip of at least 0.7 m of
   clearance (a doorway, a corridor mouth, a pinch in a cave). Cells never merge across a height
   step over 0.6 m or between flat and sloped cells, so stacked levels stay apart and staircases
   become their own zones.
4. Regions under 12 square metres (2.5 for stairs) merge into their largest neighbour.
5. Stable ids by centroid order and a class word from area and elongation: passage, corridor,
   small room, room, large hall, stairs.
6. Exits: every walkable crossing between two zones, clustered into openings, each an item in a
   "next exit" cycle that names the zone it leads into.
7. Curation, read from the same per-area JSON as the descriptions: `walls` are polylines seeded as
   zero clearance so the watershed is forced to split there, and `merges` are groups of coordinate
   anchors whose zones are unioned. Both are coordinates, never zone ids.

The thresholds were tuned by eye on floor-plan renders of the dump (`rooms_render.py`), and the
stated preference was more zones rather than fewer. Outdoor areas use the same algorithm: streets
split at narrowings, squares stay whole, and curated walls carve a big open area where it honestly
holds several places.

### How descriptions were generated

Every description was written by the assistant from screenshots of the live game, and the user
reviewed by listening. The channel split:

- Room ambiance: one title and one body per zone, anchored to a world coordinate placed on open
  floor. At runtime the zone the cursor stands in is matched against the zone each anchor stands
  in, so the data never stores a zone id and survives re-segmentation.
- Object appearance: a global table keyed by normalized prefab name, one entry per distinct prop
  reused for every instance. The affordability trick for a game built from reusable 3D assets.
- Curated details: coordinates plus prose for things that exist only as scene art (the hint
  paintings), surfaced as scan items in their own category.

The survey: a DEBUG class in the mod computes, per zone, a centroid plus farthest-point sample
spots, takes a screenshot at each with fog disabled and the HUD hidden, and writes a `data.json`
with the zone's contents split into map objects and units, plus screen-projected labels for
everything in frame. The Python driver loops it per area, downscales shots, skips surveyed zones,
and has a validate mode that checks every anchor resolves to a zone and every key has locale text.
Authoring agents then work from the files only, never driving the game.

The rules that survived real mistakes (all kept here): author only from close captures of the exact
spot; directions in prose come from coordinates, not from the picture; permanent stage only, never
units, never anything with lifecycle state; nothing invented to fill a gap; describe unflinchingly;
a title is a short noun phrase that works as a place name.

## Part 2. What is different about Disco Elysium

### The world is a painting

Disco's world is one continuously streamed painted texture laid over Unity geometry. The Unity
side holds the navmesh, the click colliders of interactable entities, the fog volumes, and the
characters; everything else a sighted player sees is paint. This changes more than any geometry
question:

- There are no props as objects. A chair, a mural, a wrecked car, a shanty wall are brushstrokes
  with no GameObject. The WOTR asset table has nothing to key on. The painted-details channel,
  a niche fix in WOTR, becomes the main channel here.
- Nothing can be isolated for a capture. There is no renderer to hide to confirm what a thing
  is; the screenshot is the whole truth, and the only truth. The compensation is that the
  interactable entities project to known screen positions, so the survey can still label a shot.
- There is no wall cutaway. WOTR's camera sliced walls open and showed neighbouring rooms inside
  a frame. Disco's painting is drawn once, from one fixed angle, with interiors drawn roof-off.
  What the frame shows is what is there; the open question per shot is only whether the ground
  seen is walkable, which the zone map answers.
- The camera yaw is fixed, so every capture is already in the canonical frame. The mod's compass
  (`WorldConvert`: Core north is Unity minus z, screen up; Core east is Unity minus x, screen
  right) is the only direction frame and applies to every shot without rotation.
- The painting is one virtual texture streamed by Amplify Texture (`AmplifyTexture.dll` plus
  its native bundle), paged in around the camera. The exterior painting's geometry is 130 tile
  meshes on a 16 m grid under `_IEG/exterior-bg-flat/ieg`, shader `FO/FO-BG`, whose materials
  hold no textures at all; an interior is a handful of tiles with walk, barrier and transparency
  layers. Unity's mipmap streaming is off. The page streamer on the `AmplifyTextureCamera`
  runtime exposes its state (`PageStreamer.SubmittedCount`, the pending queue, the pages
  uploaded this frame), and that is the settle guard: streamer idle for a frame, bounded wait.
  Measured today, a 50 m camera jump was fully painted in the first capture, under half a second
  after the move, and the streamer read idle 5 ms later. Consecutive identical captures are not a
  usable test, because characters, water and light animate every frame. The dev REPL does not
  reference the Amplify proxy, so probes there go through reflection; module code references it
  directly.
- Exteriors have their own fog of war on top of the interior zones: a `FOWController` under
  `_FOW/fow` in each scene, 150 fog meshes in Martinaise, drawing darkness beyond a radius of
  about 25 m around the character. In the end-game save it hid everything past that radius the
  moment the save loaded, so it is not a persisted explored layer either. The game's own
  `FOWController.SetVisible(false, instant: true)` lifts it for a capture and
  `SetVisible(true, true)` puts it back; both verified.
- The painting changes with story state. `Sunshine.AreaStateConfiguration.locations` holds 89
  named `Location` state machines, each with its valid states and a live `currentState`, and 576
  `StateSwitcher` components swap visuals on them. Many are paint: the hanged man (hanged, shot,
  frozen, removed), the wall graffito (ten states), the sunken car, the harbour container, the
  yard and ice eternite, the waterlock gate, the Doomed Commercial Area door and curtains, the
  tribunal (pre, post), the fishing boat, Tequila's bedroom (messy, clean), the pinball machine,
  the monuments. Where the paint under a zone or a detail changes, the prose carries a variant
  keyed by location name and state. This is the Disco form of WOTR's "story variants are separate
  entries".
- Full-resolution paint could in principle be extracted offline from the game's assets. That is a
  fallback for a shot that will not settle, not the primary source, because the offline tiles do
  not say which story state they belong to and the live frame does.

### There is no unexplored state

Disco's fog (`Sunshine.Unseen.Zone`, one volume per interior room under "fow-unrevealers", with
statuses UNSEEN, ACTIVE and INACTIVE) hides a few rooms at game start and is not used consistently.
It cannot say what a sighted player has seen: in an end-game save every one of the 42 zones read
UNSEEN until the character walked under it this session. So:

- No unexplored percentage in the zone line, no frontier scan, no "unexplored" flag on anything.
  WOTR's `FogExplored` and `FrontierModel` have no counterpart.
- The volumes are used for two things only: as an interior partition seed (a volume's footprint
  is a hand-made room boundary with a live collider when active, the Whirling kitchen's being 10
  by 9 m, so a curated wall along its edge is free), and during capture, when every UNSEEN zone
  is forced ACTIVE for the shot and restored after. Their names are also title hints: kitchen,
  union, backstage in the Whirling; gym, tower, stairs, main, storage, back in the Doomed
  Commercial Area; Cuno's, Mazov, hobo, empty, destroyed, bathroom in the Capeside apartments.
  The mod already reads them through `FogSense`.

### Scenes, floors and the one big exterior

- One exterior scene, `Martinaise-ext`, holds the whole outdoor map including the coast, spanning
  x -72 to 138 and z -258 to 2 in Unity units. Interiors are separate scenes, all 31 co-loaded,
  each in its own local coordinates (the character stands at x 5, z 7 on the Whirling's ground
  floor, metres from the origin), and the active scene's name identifies the place (the location
  readout already speaks the game's `Area Names/<scene>` term plus a floor word from the scene
  suffix).
- Only the active scene's navmesh is loaded. Walking into the Whirling swapped the mesh: the
  exterior spot the character had stood on no longer sampled, and nothing sampled 6 or 12 m above
  or below the floor. A sweep therefore sees exactly one scene and one floor with no height band
  needed.
- Unity's navmesh API in the interop exposes `SamplePosition`, `Raycast`, `CalculatePath` and
  `FindClosestEdge`, and not `CalculateTriangulation` (checked in the Mac interop today; the
  Windows interop is the same build). There are no triangles to rasterize and no triangle graph
  to read portals from.
- In-place doors carve the navmesh: the Whirling's kitchen back door carries a carving
  `NavMeshObstacle`, so a door toggle changes walkability and connectivity. Exits to other scenes
  (`TransitionEntity`) do not.

Those facts set the zone-map adapter design in Part 3. None of them changes the algorithm.

### Entities are few and unique

The interactable entities are the only objects with data, and the live set per scene is small
(about 90 accessible of 400 in Martinaise, most of the rest being clutter containers gated by
equipment or passive checks). Each is its own painting, so object appearance is one entry per
entity keyed by scene and GameObject name, with no dedupe. It is the smallest of the three
channels and comes last.

## Part 3. Zoning design

### The algorithm stays, the raster changes

The WOTR watershed ports to `NonVisualCalculus.Core.World.Zones` as pure grid math over an
abstract raster (per-cell walkable flag and height), unit-tested on synthetic floor plans: distance
transform, furniture islands, persistence watershed, height and slope gating, small-region merge,
stable ids, class words, curation walls and merges. Everything in Part 1 steps 2 to 7 is engine
free already; only step 1 and the exits touch the engine.

The module adapter builds the raster per scene by sampling:

- Bounds come from the active scene's renderers, dropping the few whose bounds are thousands of
  metres tall (backdrop planes; 126 of them in Martinaise). The per-scene `NavMeshData` in
  `NavMeshDataCollection` reports zero source bounds for most scenes, so it is not the source.
- Each cell centre is sampled with `NavMesh.SamplePosition` at the character's floor height with a
  radius of 0.3 m, and a hit that snapped more than a cell sideways is rejected. The hit's own
  height becomes the cell height, which is what the slope and height gating read. No floor band
  is needed, since only the active scene's mesh exists (Part 2).
- Cost, measured: the Whirling ground floor's 80 by 66 m renderer box is 85 thousand cells at
  0.25 m and sampled in 15 ms, yielding 183 square metres of floor; 100 thousand samples outdoors
  took 60 ms, so Martinaise at 0.25 m (about 870 thousand cells) is under a second. Sampling can
  still spread over a few frames behind a ready flag so the pump never stalls.
- Built once per scene on a retry cooldown (the mesh may not answer the frame the scene name
  changes), and rebuilt a moment after an in-place door toggles, since such doors carve the mesh
  (the mod's `DoorReader` already sees the toggle). No navmesh fingerprint is available without
  triangle access; the door hook replaces it.

Exits come from the grid alone: boundary cells between two zones at continuous height, clustered
into openings. WOTR already preferred its grid boundary over navmesh portal edges and used the
edges only as a fallback, so nothing is lost. Whether openings become scan items in the exits
category is decided in phase 2; the immediate use is naming, so a door or opening speaks the zone
it leads into.

### Tuning for Disco (phase 1 result, 2026-10-13)

Built and tuned live on the Whirling ground floor, the Capeside apartments, the Doomed Commercial
Area's second floor and Martinaise, looking at `tools/zones_render.py` floor plans of each. What
changed from WOTR's values and rules, and why:

- Persistence 0.5 m instead of 0.7. At 0.7 the Whirling's café, bar and dining room were one
  117 square metre zone; at 0.5 they split where the glass partitions pinch the floor, giving
  seven zones for the floor (dining room, bar side, hall, kitchen, union room, entrance, and the
  sealed patch behind the closed kitchen door). At 0.4 the hall fragments. Martinaise has 129
  zones at 0.7 and 153 at 0.5, with the plaza and the big yards still whole; that count is the
  naming workload.
- Interior fog volumes are partition seeds, done per cell: the tracker casts the game's own
  fog probe (a ray up on the fog layer) from every walkable cell and records the volume's id, and
  the segmentation treats a change of id between neighbours as a wall. The volumes' colliders are
  unreadable meshes and the rooms sit rotated, so a bounding box would not do. This is what
  carves the union room out of the café; the kitchen's arch already split on its own.
- Narrow ground that seeds no basin (the Doomed Commercial Area's balcony, the kitchen's aisles
  between counters) is no longer flooded into a neighbour across a partition line or left
  unlabelled: every leftover walkable component becomes its own zone, and a small zone with no
  neighbour to merge into stays a zone down to one square metre, below which it is sampling noise
  and drops. WOTR dropped every isolated tiny region, which in Disco left the character standing in
  "no zone".
- Doors behind which the mesh is carved come out as sealed zones with no exit (three of the nine
  Capeside flats, the kitchen back door), which is correct until the door opens and the rebuild
  joins them. The dark room in the Doomed Commercial Area is a 4 square metre island for the same
  reason: its darkness seals the rest of the floor, and the game's own paths agree.
- Everything else kept WOTR's values: furniture islands up to 12 square metres, minimum zone
  area 12, stair detection, the class-word thresholds.

Curated walls and merges remain the tool for the plaza, the harbour yard, the coast and any
street the heuristic splits oddly; none were needed to get here.

### What the game cannot give us

The decompile was checked for a ready-made zoning source and there is none. `Sunshine.Location`
is a story-state machine, not geometry. `PositionStateLocation` places things per state. The
reverb settings are assets, not volumes. The fog volumes cover a handful of interior rooms. The
journal map names districts but carries no walkable boundaries. So the navmesh watershed is the
right basis, the same conclusion WOTR reached, with the fog volumes as a seed where they exist.

### Zone naming

Every zone in every scene gets an authored title; that is the user's decision and it is
affordable because the map is small (one exterior and 31 interiors, most of them one to six
rooms; WOTR's Kenabres alone has 124 zones). A zone speaks its title when an anchor with one
resolves inside it. The "Room N" plus class-word line of WOTR survives only as the development
fallback for a zone not yet named, and the validator reports every such zone in a surveyed scene
as a failure, so no shipped scene has one. The class words and the fallback template still go in
the central strings table; the titles live in the description files.

Naming happens before describing: the zone-naming pass for a scene is the first authoring step
after its segmentation is tuned, so the exits cycle, the location readout and the arrival line
speak real place names from phase 2 on, before any body text exists.

## Part 4. Description channels

1. Zone ambiance. One title and body per zone, anchored to a world coordinate on open floor. The
   title is a short noun phrase that doubles as the zone's spoken name ("the Whirling's
   kitchen"). The body is two to five sentences on the permanent paint: geometry, floors,
   furniture, light, decals, wreckage, atmosphere. More than one anchor only for a genuinely
   distinct part of one zone.
2. Painted details. Coordinate points with prose for things that exist only as paint: a mural,
   graffiti, a wreck, a shrine, a shop sign, a view. Each is a scan item in a new `detail`
   category: it cycles, sonifies, carries bearing and distance, and can be walked to like
   anything else. Central in Disco, because most of what a sighted player sees is paint with no
   data. The survey finds them while looking at the zone shots, and a detail is listed only where
   a sighted player would notice it from walkable ground.
3. Object appearance. One entry per interactable entity, keyed by scene plus GameObject name, spoken
   for the focused scanner item. Size, material, colour, condition, what it sits on or against.
   Never its state, contents or meaning; the game's examine conversation and the orbs already say
   what a thing is and what Harry thinks, and only very rarely how it looks.

Story-state variants: an entry in any channel may carry a per-state variant, keyed by the
`Sunshine.Location` state name, and the loader picks the variant matching the live state, falling
back to the base text. The survey records the state each shot was taken in.

Strict separation from game text: mod prose is spoken only by its own keys and announcements,
never appended to an orb, an examine line or any other game text, so a sentence heard as the
game's is always the game's.

## Part 5. Data and surfacing

### Files

Per scene, under `assets/descriptions/`:

- `<scene>.json`: geometry and anchors only. `walls` and `merges` for curation, `rooms` as
  `{x, y, z, key}` anchors, `details` as `{x, y, z, key}` points, `objects` as the list of entity
  names that have text. Coordinates in the game's world frame. Optional `state` on any entry.
- `<scene>.<lang>.txt`: the prose, in the same key = value format as `lang/<language>.txt`, so the
  existing parser and the translator workflow are reused. Keys `room.<key>.title`,
  `room.<key>.body`, `detail.<key>`, `object.<name>`, with `@<state>` appended for a variant.
  English is the fallback for a missing key, matching `LanguageSync`.

Loaded by a module `DescriptionStore` on scene change and on F6, following the game language. This
is mod data, not game state, so holding it in memory is allowed. Anchors are matched to zones
live, at speak time, never stored as zone ids.

### Keys and announcements

- Describe object (focused scanner item) and describe zone (the zone under the cursor), each
  answering "nothing described" when there is no text. The focused item is already filtered by
  the game's own accessibility, so a gated object cannot leak.
- The `detail` category joins `WorldTaxonomy.Scan`, folded into the Items quick-nav group; the
  scanner cycle enumerates the list, and the sonar toggle needs one new setting in
  `ModSettings.SonarCategoryEnabled`, whose default branch would otherwise sound it unasked.
- The location readout (R) and the automatic arrival line gain the zone after the area name.
- Zone-change announcement from the world tick: when the cursor (else the player) resolves to a
  different zone, speak the zone line, queued, behind a setting on by default. The hook sits in
  `WorldReader.Tick` beside the existing scene-change reposition check.
- Keys, decided by the user: E describes the focused object and Shift+E describes the zone; X
  stays read-experience.
- Every framing word (the nothing-described line, the class words, the "Room N" template, the
  category name "details") goes in the strings table. The prose lives in the description files.

## Part 6. Survey tooling

A DEBUG-only `DevSurvey` class in the module, driven through `/eval` by a Python driver ported from
WOTR's, with four modes.

- Rooms: per zone, the centroid plus farthest-point spots (count scaling with area, capped), one
  screenshot each, and a `data.json` with the zone's entities split into people and things, the
  anchor candidates, the live `Location` state, and screen-projected labels for every entity in
  frame. Labels say where entities are; they never name paint.
- Details: coordinates chosen while reading the zone shots, one tighter shot each.
- Objects: one framed shot per entity. Inactive story twins (`objects --inactive`: the
  containers this save has looted and the props its story removed, props and containers only,
  one capture per distinct name) are switched on for the shot and off again, nothing written to
  the save; safe because the prose is pure appearance and only ever spoken once the game has
  made the object focusable.
- Validate: offline, every anchor and detail key has prose in every shipped language, every
  object name exists in the survey list, no orphan keys, no duplicates; live, every anchor
  resolves to a zone, every zone in a curated scene has a title (an untitled zone next to titled
  ones is a curation bug), every object key matches a live entity.

Capture mechanics, all exercised live today on the Whirling ground floor:

- Camera: set `IsSlaved` false, then `SetFocus(point, new Il2CppSystem.Nullable<float>(factor),
  instant: true)` moves the camera that frame; restore by `SetFocus` to the saved focus and zoom
  and `IsSlaved` true, which snaps back to the player. Not `SetSpecialSlave`, the inventory path,
  which drags the player.
- Zoom: the factor is clamped to the area's zoom limits, and the mod's own `PinZoom` holds the
  zoom at the area maximum every frame while it drives the world, so the survey suspends
  `PinZoom` for the session. At the Whirling's maximum (orthographic size 5.7) one frame covers
  the whole ground floor at 3456 pixels wide, which is a good room shot; detail and object shots
  need the pin suspended and a smaller factor.
- HUD: disable the `FinalUICamera` camera (the Global UI Canvas renders through it) and the
  `Camera Space Canvas` component for the frame; the capture came back with no HUD at all.
  Re-enable both unconditionally in restore.
- Fog: `FOWController.SetVisible(false, true)` for the scene's darkness radius, and every UNSEEN
  `Unseen.Zone` forced ACTIVE for the frame with the old status written back after. Without the
  first, any framing more than about 25 m from the character captures black, which is what every
  far capture produced before it was found.
- Settle: wait until the Amplify page streamer is idle for a frame (Part 2), bounded. Today's
  measurement says the paint is there by the first capture, so the guard is cheap insurance. A
  frame outside the painting's extent is black by design, which is what a framing beyond the
  interior's edge produced, not a streaming failure.
- Two fixed dark shapes sit in the bottom corners of every capture with the HUD off, the same in
  interiors and outdoors. They are not parented to the camera; identifying and hiding their
  source is a phase 4 item, and until then the survey keeps its subject out of those corners.
- The dev server's `/screenshot` returns the path to an 11 MB PNG; shots are downscaled to about
  1400 pixels wide for review.
- Labels: every entity's position projects through the main camera (orthographic, yaw 180,
  pitch 35) to a screen point, so the survey annotates shots the way WOTR did.

Authoring agents work from the files only. Nothing in the pipeline expects the user to write
prose; the user is the reviewer and the arbiter of register and detail.

## Part 7. Authoring rules for Disco

The WOTR checklist adapted. The full rules file is written in phase 3 and handed to authoring
agents verbatim.

- Author from close captures of the exact spot. Nothing inferred from the corner of a wide shot,
  nothing invented to fit a label or fill a gap. If the paint does not show it, it does not exist.
- Permanent paint only: geometry, furniture, floors, props, decals, light sources, atmosphere.
  Never people, never anything with lifecycle state, never loot, never anything Harry can change
  except as a story-state variant.
- Appearance, not meaning. What it looks like, not what it is for or signifies. The game says
  the rest, and the mod never restates the game's writing.
- No compass directions or distances in prose. The mod speaks bearing and distance live from the
  anchor; prose uses relative placement only ("against the far wall", "under the window").
- Nothing tied to the hour. Disco has a day cycle and the paint's light does not change with it,
  but the sky and weather overlays do; captures are taken at one time and prose must not describe
  that moment as permanent.
- Walkability is checked against the zone map before prose implies traversal. A painted stair or
  door with no navmesh under it is described as out of reach.
- Unflinching. A mature game; describe what is there, plainly.
- Titles are short noun phrases that work as a place name in an exit readout and a location line.

## Part 8. Phases

1. Zone map. Done 2026-10-13: `NonVisualCalculus.Core.World.Zoning` (the segmentation, with 14
   tests on synthetic plans), the module's `ZoneTracker` (flood-fill navmesh sampling, fog
   partitions, door rebuild hook), the `DevZones` hooks and `tools/zones_render.py`. Zones
   resolve at the character and the cursor in every scene visited. The dev REPL now compiles
   against the live module assembly, so module classes are callable from `/eval` by name.
2. Zone naming and surfacing. Done 2026-10-14: the description store, the zone in the location
   readout and the arrival line, the zone-change announcement with its toggle, and names for
   every zone of the Whirling's ground floor and Martinaise (153). Openings as exit items are
   not built: doors and scene exits already name their destination, and the zone-change line
   covers the opening itself.
3. Store, keys and rules. Done 2026-10-14: `descriptions/<scene>.txt` and `<scene>.<lang>.txt`,
   the loader following scene and language, story-state variants, E and Shift+E, the details
   category with its sonar toggle, the strings, the offline validator in the test suite, and
   `docs/description-rules.md`.
4. Survey tooling. Done 2026-10-14: `DevSurvey` and `tools/survey.py` with zones (outlined
   captures), objects (active entities, and the inactive early-game twins with `--inactive`),
   shot, entities, states and variants modes; HUD hide, fog lift, zoom-pin suspend and the
   streamer settle check; `GroundAt` to place a detail from a capture's pixel. A capture is lit
   as noon (the day-night visual is unlocked and set, then put back), every `Unseen.Zone` is
   forced ACTIVE (INACTIVE, an entered-and-left room, draws black too), the level designers'
   inactive "GRAND LIGHT FOR LEVEL EDITING" is switched on where a scene has one, the flashlight
   gate's Lua boolean is set so the dark floors uncover, and the frame's focus sits at the game's
   own focus height (a floor-height focus puts the camera into the floor). `--teleport` warps the
   character beside each entity first, so the game cuts the roof away as in play, and frames on
   the character's navmesh spot rather than the entity's transform (a wall shelf's own position
   aims the camera into the void behind the wall). Everything is restored by `Restore`, and the
   saved state is dropped on a scene change.
5. Content pass. Done 2026-10-14 for every zone of all 31 scenes (named and described), the
   painted details and object descriptions of Martinaise and every interior, and the story-state
   variants where the paint changes (Martinaise and the interiors, from the variants captures).
   The inactive early-game twins are described too (captured with `objects --inactive`), and
   every interior was recaptured lit, uncovered and roof-cut and its lines corrected against
   those captures. The interiors' painted details are the Whirling's mosaic, chalkboard and
   arches, the Doomed floor's chalked board, the apartments' graffiti wall and the shack's
   pig's head and shark painting. Remaining: the listening review of register.

## Part 9. Verified live (2026-10-13, end-game save, day 8)

Checked through the dev server, first in Martinaise and then inside the Whirling after walking
the character through its door with the scanner's exits cycle and the interact verb (the real
player path). The findings are folded into the sections above; this is the list.

- Scenes: 31 game scenes plus the collage and lobby shells are all loaded at once, each interior
  in its own local coordinates.
- Navmesh: only the active scene's mesh is loaded; a scene change swaps it. Sampling cost is 60 ms
  per 100 thousand calls; the Whirling ground floor sweeps in 15 ms.
- Doors: the Whirling kitchen back door has a carving `NavMeshObstacle`; scene exits have none.
- Fog: 42 `Unseen.Zone` volumes across the interiors, named per room, all UNSEEN in the save
  until entered; a zone's collider gives the room footprint while active.
- Story states: 89 `Location` machines with live `currentState`, 576 `StateSwitcher`s.
- Painting: Amplify Texture virtual texturing over 130 exterior tile meshes; a 50 m jump was
  painted by the first capture and the page streamer read idle 5 ms after the move. Consecutive
  captures of a still camera differ (animation), so identity is not a settle test.
- Exterior fog: the `FOWController` darkness beyond about 25 m of the character made every far
  capture black until `SetVisible(false, true)` lifted it; restore verified.
- Camera and HUD: the `SetFocus` and `IsSlaved` path moves and restores cleanly; the mod's
  `PinZoom` clamps zoom while driving; disabling `FinalUICamera` and the `Camera Space Canvas`
  gives a HUD-free capture, confirmed by viewing the frame.
- Python tooling: numpy and Pillow are installed globally in the default uv-managed Python (the
  user's choice; the interpreter is externally managed, so installs need the override flag), and
  plain `python3` runs the survey driver and renderer.

## Risks

- Interior over-segmentation and outdoor under-segmentation, handled by the tuning pass and by
  curated walls and merges; the renderer makes both visible before any prose is written.
- Paint that never settles for a far framing, which would make a capture show empty tiles. The
  tile-state settle check and the offline-tile fallback cover it.
- The naming pass is a real cost on top of WOTR's model: every zone, including street segments,
  needs a name the user will hear on every entry. The floor-plan render and the room shots make
  it a batch job per scene, and the user's review of the first two scenes sets the style.
- Story-state variants multiply the content where the paint changes. The survey records the
  state so the first scene shows how many zones are affected.
- Content volume. Details are one per noticeable thing across every scene and the long pole. The
  incremental survey and per-scene commits keep it tractable.
- Drift with game updates is low: the game is patch-frozen and anchors are coordinates.
