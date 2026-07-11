using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.Audio;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.World.Overlays.Systems
{
    /// <summary>
    /// The cursor's sense of the things it glides over. Two halves, the WOTR <c>ObjectCueSystem</c> model:
    ///
    /// - While gliding, a short stereo blip each time the cursor crosses into or out of a thing's footprint
    ///   (a rising click on entering one, including swapping straight from one thing to another; a falling
    ///   click on leaving to bare ground), panned toward the thing. Gliding is too fast to narrate, so the
    ///   blip is all that sounds during motion.
    /// - On a glide stroke ending (the overlay's point readout), it contributes the name of the thing under
    ///   the cursor, so the player hears "crate; northeast, 2 meters" - the name first, then the position
    ///   from the spatial system.
    ///
    /// It senses the actionable set - accessible interactables and world-anchored orbs whose conditions are
    /// met - the exact set the Enter verb acts on, so what the cursor names and clicks can never disagree:
    /// scenery you cannot act on is not sensed. Self-gates like the audio systems - the blip falls silent
    /// under a cutscene, a lost-control moment, or a menu floating over the world.
    /// </summary>
    public sealed class ObjectCueSystem : OverlaySystem
    {
        // The slack around a thing's real footprint within which the cursor counts as "on" it. Small, because
        // the footprint (Bounds) now carries the thing's actual size, so this is only navmesh-clamp gap: the
        // freeform cursor is clamped to walkable ground and a body's footprint edge can sit just off the mesh
        // (a door in a wall, a prop against it), so the cursor only ever gets near the edge, never onto it.
        // Bounds.NearestPoint returns distance 0 inside the shape, so a cursor over any part of a wide thing
        // is on it. One margin for the blip, the spoken name, and Enter, since all three call Under().
        public const float HoverMargin = 0.75f;
        // A thing this close to the player IS the player (the player's own entity, if it is in the registry);
        // never hover-announce the character you are standing on when the cursor is centred.
        private const float PlayerEpsilon = 0.5f;
        // How far above or below the cursor's ground level a thing's body may sit and still count as on the
        // cursor's own level, no questions asked - pivot slack for things standing right here (a door's
        // mid-panel pivot, a lever above waist height), NOT a cross-level allowance: Martinaise stacks its
        // levels as tight as 2 m (the Whirling balcony floats just over the front door's head), so anything
        // past this slack must instead prove it belongs to reachable ground (IWorldItem.ReachableFrom) - a
        // staircase or step-exit connects and is kept; the balcony door over the plaza does not and is
        // dropped. Public because the scanner applies the same paired gate (Scanner.Offered), so the two
        // senses agree on what counts as another level.
        public const float SameLevelSlack = 1f;
        // Crossover distance for the blip pan: close in the pan tracks the lateral offset, far out it
        // saturates toward the bearing.
        private const float PanWidth = 3f;
        // Public so the learn-sounds menu previews the blips at exactly this level.
        public const float CueVolume = 0.7f;

        // Cursor travel this frame below this is "not moving" (flicker/jitter); above MaxGlideStep is a jump
        // (a recenter or area change), not a glide, so neither counts as crossing a footprint.
        private const float MoveEpsilon = 0.005f;
        private const float MaxGlideStep = 2f;

        private readonly IWorldModel _model;
        private readonly SpatialSources _cues;

        // The thing the cursor is currently inside (nearest), or null; compared by reference across frames
        // (the registry keeps one stable proxy per object). Baselined on the first active frame so arriving
        // in the world does not fire a spurious blip.
        private IWorldItem? _inside;
        // Last frame's candidate, awaiting one-frame confirmation before it replaces _inside. A footprint
        // crossing persists across frames, so requiring two frames running rejects a single-frame streaming
        // flicker (a SenseOrb popping in and out around the moving cursor) that would otherwise phantom-blip.
        private IWorldItem? _pending;
        private bool _baselined;
        // Last frame's cursor position, to tell a real glide from a still cursor: a footprint crossing is a
        // motion event, so the blip only fires while the cursor is actually moving. A still cursor tracks the
        // thing under it silently, so flicker in the surrounding set (an orb streaming in and out) never clicks.
        private Vector3 _lastCursor;
        private bool _haveLast;

        public ObjectCueSystem(IWorldModel model, SpatialSources cues)
        {
            _model = model;
            _cues = cues;
        }

        public override string Name => WorldSystemObjectCue;
        public override string Key => "objects";

        // The blip is move-driven but the spoken name fires on stop, so "when moving" would suppress half its
        // job - Off/Continuous only.
        private static readonly PlayMode[] OffContinuous = { PlayMode.Off, PlayMode.Continuous };
        public override IReadOnlyList<PlayMode> SupportedModes => OffContinuous;

        public override void OnExit(Overlay overlay)
        {
            _inside = null;
            _pending = null;
            _baselined = false;
            _haveLast = false;
        }

        public override void Tick(float dt, Overlay overlay)
        {
            // Stand down (and re-baseline, so re-entry is silent) when the gate is closed, control is lost,
            // or a menu floats over the world - the same gate the wall tones use.
            if (!ShouldPlay(overlay) || !overlay.HasControl || !overlay.InputActive)
            {
                _inside = null;
                _pending = null;
                _baselined = false;
                _haveLast = false;
                return;
            }

            Vector3 cursor = overlay.Cursor.Position;
            Vector3 player = overlay.Cursor.PlayerPosition;
            IWorldItem? candidate = Under(cursor, player);

            // A footprint crossing only happens while gliding: count this frame as a move when the cursor
            // travelled a glide-sized step. A still cursor (jitter below MoveEpsilon) or a jump (a recenter,
            // above MaxGlideStep) does not click - it just tracks the thing under it silently below.
            float travel = _haveLast ? Geo.Distance(cursor, _lastCursor) : 0f;
            bool moved = _haveLast && travel > MoveEpsilon && travel < MaxGlideStep;
            _lastCursor = cursor;
            _haveLast = true;

            if (!_baselined) { _inside = candidate; _pending = candidate; _baselined = true; return; }

            // Confirm the candidate across two frames before it replaces _inside: a real crossing lingers,
            // but a streamed-in/out orb flickers for a single frame and never confirms, so it makes no blip
            // even while the cursor is moving (the move gate alone would let that phantom through).
            IWorldItem? confirmed = ReferenceEquals(candidate, _pending) ? candidate : _inside;
            _pending = candidate;
            if (ReferenceEquals(confirmed, _inside)) return;

            // Entered a thing (incl. swapping object to object): rising click. Left to bare ground: falling
            // click. Placed toward whichever thing the cue is about (one of the two is non-null whenever
            // they differ): the nearest part of its footprint relative to the player, the same origin the
            // spoken bearing uses, so the blip sounds where the readout will say the thing is - and a
            // tracked source, so it keeps following while it plays (the player can be mid-walk). Only
            // sound it on a real glide; a still cursor updates silently.
            if (moved)
            {
                IWorldItem about = (confirmed ?? _inside)!;
                _cues.Play(confirmed != null ? AudioCue.CursorEnter : AudioCue.CursorExit,
                           () => overlay.Cursor.PlayerPosition,
                           p => about.Bounds.NearestPoint(p),
                           _ => CueVolume,
                           PanWidth);
            }
            _inside = confirmed;
        }

        public override IEnumerable<OverlayAnnouncement> Announce(OverlayContext ctx)
        {
            if (!Enabled || ctx.Want != AnnouncementContext.Point) yield break;
            // The name is the same Under() the blip and Enter use, so a stop names exactly the thing Enter
            // would act on. Recomputed here (not read off the blip's confirmed state) so it is fresh after a
            // recenter, where the cursor jumps without a glide Tick.
            IWorldItem? under = Under(ctx.Cursor, ctx.Reference);
            // The name carries its state via the shared composition (ItemLabel), so the cursor and the
            // scanner can never disagree about a door standing open.
            if (under != null && !string.IsNullOrEmpty(under.Name))
                yield return new OverlayAnnouncement(AnnouncementContext.Point, ItemLabel.For(under, under.Name));
        }

        // The one thing under the cursor: the nearest actionable thing whose footprint the cursor is within
        // HoverMargin of, not the player's own entity. The single selection the blip (Tick), the spoken name
        // (Announce), and the Enter verb (WorldReader) all call, so they cannot disagree. The set is
        // IsAccessible - accessible interactables and orbs alike, the exact set Enter can act on - so
        // inaccessible scenery is never named or clicked. Scans the registry each call (a few hundred items).
        public IWorldItem? Under(Vector3 cursor, Vector3 player)
        {
            IWorldItem? best = null;
            float bestDist = HoverMargin;
            float bestBody = float.MaxValue;
            foreach (IWorldItem it in _model.Items)
            {
                // Accessible AND visible: what a sighted player could see and act on. An accessible thing
                // inside an unrevealed room (a door behind a closed door, hidden in the fog-of-war black)
                // is neither named nor clicked until the room reveals.
                if (!it.IsAccessible || !it.IsVisible) continue;
                // Drop anything sitting on the player - that is the character's own entity - unless the thing
                // legitimately rides the character (a thought-cabinet orb orbiting it), which must stay findable.
                if (!it.RidesPlayer && Geo.Distance(it.Position, player) < PlayerEpsilon) continue;
                // Distance to the footprint's nearest part, XZ-only: whether the cursor is over a thing is a
                // flat-map question, so a thing whose geometry sits up high (a staircase, an exit whose trigger
                // origin floats above the steps) is still on the cursor gliding beneath it. An exact tie - the
                // cursor inside several overlapping footprints reads distance 0 to each (a crate inside a wide
                // disc, two abutting boxes) - goes to the thing whose BODY is nearest the cursor, so the more
                // specific thing wins and a wide footprint can never permanently shadow a small one sitting
                // inside it. Geometry decides, so the pick is stable however the poll reorders the registry.
                Vector3 np = it.Bounds.NearestPoint(cursor);
                float d = Geo.DistanceXZ(np, cursor);
                float body = Geo.DistanceXZ(it.Position, cursor);
                if (d > bestDist || (d == bestDist && (best == null || body >= bestBody))) continue;
                // Height-reachability gate. XZ-only detection would also put the cursor "on" a thing hanging
                // well above (or below) the ground it is clamped to - the Whirling balcony door overhanging the
                // plaza reads as under the cursor though its body sits metres overhead. An off-slack or
                // click-priced candidate (its reach verdict trustworthy - the sealed-room pinball, the balcony
                // door) must be genuinely Reachable; a same-level markerless candidate is kept unless its
                // refusal is trustworthy (a Severed path over located ground - the sealed backroom box - drops
                // it, while a ground-finder miss keeps it). So a staircase or step-exit on connected ground is
                // kept, an NPC behind a bar counter (a cross-level conversation priced from below) is kept
                // through the person fallback, an orb overhead reads Reachable and is kept, and only a proven
                // severance hides a thing. The verdict runs for the current best candidate only, not per item.
                ReachState reach = it.ReachableFrom(player);
                bool strict = System.Math.Abs(np.Y - cursor.Y) > SameLevelSlack || it.ReachIsClickPriced;
                if (strict ? reach != ReachState.Reachable : reach == ReachState.Severed) continue;
                bestDist = d; bestBody = body; best = it;
            }
            return best;
        }

    }
}
