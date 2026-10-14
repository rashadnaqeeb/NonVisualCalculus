# Description authoring rules

The checklist for writing zone names, zone bodies, painted details and object descriptions
(`descriptions/<scene>.txt` and `descriptions/<scene>.en.txt`). Hand this file to any authoring
pass verbatim. Design background: `zones-and-descriptions-plan.md` (same folder).

## Ground truth

- Author from the survey captures of the live game only: `python3 tools/survey.py zones` for the
  zone shots with the zone's outline drawn on them, `objects --teleport` for one tight shot per
  entity (`--inactive` for the early-game twins), `shot --at X,Z` for a detail. Never from the
  game's asset files, which carry no story state.
- A capture that comes out black or dim is not a licence to write less: the survey lights every
  scene as noon, uncovers every room and warps the character beside the thing; if a capture still
  does not show the subject, find out what hides it and recapture. Review tight crops on a
  contact sheet with auto-levels per tile; a room lit by one lamp reads once stretched.
- Every claim needs a capture that shows it inside the outlined zone. The isometric painting shows
  the neighbouring zone in the same frame; the red outline says what is in this one. Nothing is
  inferred from the corner of a wide shot or invented to fit a name.
- Walkability is the zone map's, not the picture's. A painted stair, door or path with no zone
  cells under it is not a way through; if it is striking, describe it and say it is out of reach.
- Coordinates come from the survey data (the zone's survey spot for an anchor, `DevSurvey.GroundAt`
  from a capture's pixel for a detail), in the mod's world frame, never typed from memory.

## What goes in

- The permanent paint: ground, walls, furniture, props, wrecks, decals, light sources, plants,
  snow and ice as painted. What a sighted player sees standing there.
- Appearance, never meaning. What a thing looks like, not what it is for, who owns it or what it
  signifies. The game's examine conversations and orbs say the rest, and the mod never restates
  the game's writing.
- Unflinching detail. A mature game; blood, wreckage, squalor and cruelty are described plainly.

## What stays out

- People and anything that moves: characters, animals that wander, vehicles that drive away.
- Anything with lifecycle state: loot, containers' contents, open or locked state, lit or dark,
  anything a script toggles. A thing whose look changes with the story gets a per-state variant
  (`key@LOCATION:state`), never a description of one state as permanent.
- Compass directions and distances. The mod speaks bearing and distance live from the anchor;
  prose uses relative placement only ("against the far wall", "under the window").
- The time of day and the weather. Captures are taken at one hour; the sky and the lighting are
  not the zone.

## Titles

- A zone's title is a short noun phrase that works as a place name in an exit readout, the
  location line and a one-word arrival announcement: "the plaza", "the kitchen archway", "the
  path up the islet". Lowercase except proper names, with the article.
- Every zone gets a title before any zone gets a body. The validator fails a scene with an
  untitled zone.
- Titles are unique within a scene and name what distinguishes the zone from its neighbours.
  Two stretches of the same street are "the lorry queue" and "the lorry crossing", not "the
  street" twice.
- A detail's title is the thing itself ("floor mosaic", "chalkboard menu"); the scanner speaks it
  as the item's name.

## Bodies

- Two to five sentences, concrete nouns, the materials and colours the painting shows. Lead with
  the one thing that places the listener, then the rest.
- No lists of everything. Summarize the mundane, surface the striking.
- Avoid dashes and fancy punctuation; the reader speaks them.

## Files

- `<scene>.txt` holds geometry only: `zone <key> x y z`, `detail <key> x y z`,
  `object <key> = <entity name>`, `wall`, `merge`. Keys are short lowercase identifiers.
- `<scene>.en.txt` holds the English prose under `zone.<key>.title`, `zone.<key>.body`,
  `detail.<key>.title`, `detail.<key>.body`, `object.<key>`. Another language goes in
  `<scene>.<lang>.txt` with the same keys; English is the fallback per key.
- `dotnet test` validates every committed scene offline: every anchor has a title, every detail
  and object has text, no orphans, no duplicates. Live, `DevZones.Stats()` and the untitled-zone
  check in the plan's phase 2 confirm every zone resolves.
