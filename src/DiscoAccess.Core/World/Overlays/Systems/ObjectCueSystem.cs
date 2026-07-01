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
    /// It reads the full visible set (scenery included): the cursor is the look-around sense, while the sonar
    /// and scanner read only the actionable set. Self-gates like the audio systems - the blip falls silent
    /// under a cutscene, a lost-control moment, or a menu floating over the world.
    /// </summary>
    public sealed class ObjectCueSystem : OverlaySystem
    {
        // How near the cursor must be to a thing's nearest part for the glide blip to count it "on" it. A
        // tight footprint radius, since the proxies report point bounds for now; once real footprints are
        // wired, Bounds.NearestPoint returns distance 0 inside the shape, so the same test still holds.
        private const float HoverRadius = 1.5f;
        // The wider radius the name-on-stop uses to pick the thing under the cursor. It matches the Enter
        // verb's snap radius (WorldReader.SnapRadius is defined as this), so a glide that stops leaves nothing
        // Enter could act on unnamed: the 1.5-2.5 m band where Enter snaps but the tight blip never fired is
        // still spoken. Kept a touch generous because the freeform cursor is navmesh-clamped and a body can
        // sit just off the mesh.
        public const float ReachRadius = 2.5f;
        // Ignore things more than this far above/below the cursor (another level of the scene).
        private const float LevelGap = 3f;
        // A thing this close to the player IS the player (the player's own entity, if it is in the registry);
        // never hover-announce the character you are standing on when the cursor is centred.
        private const float PlayerEpsilon = 0.5f;
        // Crossover distance for the blip pan: close in the pan tracks the lateral offset, far out it
        // saturates toward the bearing.
        private const float PanWidth = 3f;
        private const float CueVolume = 0.7f;

        // Cursor travel this frame below this is "not moving" (flicker/jitter); above MaxGlideStep is a jump
        // (a recenter or area change), not a glide, so neither counts as crossing a footprint.
        private const float MoveEpsilon = 0.005f;
        private const float MaxGlideStep = 2f;

        private readonly IWorldModel _model;
        private readonly IAudioEngine _audio;

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

        public ObjectCueSystem(IWorldModel model, IAudioEngine audio)
        {
            _model = model;
            _audio = audio;
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
            IWorldItem? candidate = FindUnder(cursor, player, HoverRadius);

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
            // click. Pan toward whichever thing the cue is about (one of the two is non-null whenever they
            // differ). Only sound it on a real glide; a still cursor updates silently.
            if (moved)
            {
                IWorldItem about = (confirmed ?? _inside)!;
                _audio.PlayCue(confirmed != null ? AudioCue.CursorEnter : AudioCue.CursorExit, CueVolume,
                               PanFor(about, player));
            }
            _inside = confirmed;
        }

        public override IEnumerable<OverlayAnnouncement> Announce(OverlayContext ctx)
        {
            if (!Enabled || ctx.Want != AnnouncementContext.Point) yield break;
            // Name to the wider Enter reach, not the tight blip footprint, so a stop never stays silent over a
            // thing Enter would act on.
            IWorldItem? under = FindUnder(ctx.Cursor, ctx.Reference, ReachRadius);
            if (under != null && !string.IsNullOrEmpty(under.Name))
                yield return new OverlayAnnouncement(AnnouncementContext.Point, under.Name);
        }

        // The nearest visible thing within <paramref name="radius"/> of the cursor, on the cursor's level,
        // that is not the player's own entity. Scans the full registry each call (a few hundred items), like
        // WOTR. The blip passes the tight HoverRadius; the name-on-stop passes the wider ReachRadius.
        private IWorldItem? FindUnder(Vector3 cursor, Vector3 player, float radius)
        {
            IWorldItem? best = null;
            float bestDist = radius;
            foreach (IWorldItem it in _model.Items)
            {
                if (!it.IsVisible) continue;
                Vector3 body = it.Position;
                if (System.Math.Abs(body.Y - cursor.Y) > LevelGap) continue;     // another level
                if (Geo.Distance(body, player) < PlayerEpsilon) continue;        // the player itself
                // Strict less-than so an exact tie keeps the first-seen item rather than flapping between
                // two coincident things as the registry's enumeration order shifts on its poll.
                float d = Geo.Distance(it.Bounds.NearestPoint(cursor), cursor);
                if (d < bestDist) { bestDist = d; best = it; }
            }
            return best;
        }

        // Pan for a thing's blip: its nearest part's lateral offset from the player, the same origin the
        // spoken bearing uses, so the blip places the thing where the readout will say it is.
        private static float PanFor(IWorldItem item, Vector3 player)
        {
            Vector3 np = item.Bounds.NearestPoint(player);
            float dx = np.X - player.X, dz = np.Z - player.Z;
            float dist = (float)System.Math.Sqrt(dx * dx + dz * dz);
            return Spatial.Pan(dx, dist, PanWidth);
        }
    }
}
