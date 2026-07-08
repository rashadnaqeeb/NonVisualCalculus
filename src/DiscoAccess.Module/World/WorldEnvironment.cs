using DiscoAccess.Core.World.Overlays;
using FortressOccident;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.AI;
using Snv = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// The <see cref="IWorldEnvironment"/> over the live game: the overlay framework reads the player's
    /// position, whether the player has control, the navmesh clamp, the visible-frame bound, and the
    /// fog-of-war state through here. This is the thin engine-touching adapter the Core world layer is kept
    /// free of; it converts Unity's <c>Vector3</c> to <see cref="System.Numerics.Vector3"/> at the boundary
    /// so no Unity type crosses into Core.
    /// </summary>
    internal sealed class WorldEnvironment : IWorldEnvironment
    {
        /// <summary>The main party character's live transform position (the readout origin). The transform
        /// is the freshest source — the data position lags it during a move — and Zero before a game loads.</summary>
        public Snv PlayerPosition
        {
            get
            {
                Character main = Main;
                return main != null ? WorldConvert.ToSnv(main.transform.position) : default;
            }
        }

        /// <summary>The player controls the character when one exists, no conversation is up, and the
        /// game itself is accepting world input. The input-lock read is the game's own click gate
        /// (<c>GameController.inputLocks</c>, which its ground- and entity-click paths check before
        /// moving): every scripted sequence - a dialogue's outro animation, a sequencer camera move, a
        /// door transition, quicktravel - holds a lock for exactly its duration, so this stays false
        /// through the window after a conversation ends while the scene still animates, and flips true
        /// the moment the game would accept a click again. The static cutscene flag covers the staged
        /// full-scene situations (dreams, day starts) for their whole lifetime, wider than the locks
        /// they hold while performing. The world reader already only ticks on the in-game (CLEAR) view,
        /// so this is the finer cutscene/dialogue gate on top of that.</summary>
        public bool HasControl
        {
            get
            {
                if (Main == null || DialogueManager.isConversationActive) return false;
                if (Sunshine.CutsceneSituation.CUTSCENE_SITUATION_ACTIVE) return false;
                Sunshine.GameController gc = Sunshine.GameController.Singleton;
                return gc != null && !gc.IsWorldInputDisabled();
            }
        }

        /// <summary>Whether a player character exists at all (a game is loaded) - position reads are
        /// meaningless without one.</summary>
        public bool HasPlayer => Main != null;

        /// <summary>Whether the character could walk between two points: both ends snapped onto the
        /// navmesh and a COMPLETE path between them (the entity reachability gate's oracle, for a bare
        /// stored point such as a bookmark). A point whose ground was never walkable, or that a later
        /// game state severed, reads unreachable.</summary>
        public bool PathComplete(Snv from, Snv to)
        {
            if (!NavMesh.SamplePosition(WorldConvert.ToUnity(from), out NavMeshHit start, PathSnapRadius, AllAreas))
                return false;
            if (!NavMesh.SamplePosition(WorldConvert.ToUnity(to), out NavMeshHit end, PathSnapRadius, AllAreas))
                return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(start.position, end.position, AllAreas, path)
                   && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>The next corner of the character's own walk from <paramref name="from"/> to
        /// <paramref name="to"/> (both snapped onto the mesh): the first path corner meaningfully past the
        /// start, or the destination itself on a straight path - the "which way do I start walking" point
        /// the scanner's waypoint readout speaks. False when no complete path connects the points.</summary>
        public bool NextPathLeg(Snv from, Snv to, out Snv corner)
        {
            corner = default;
            if (!NavMesh.SamplePosition(WorldConvert.ToUnity(from), out NavMeshHit start, PathSnapRadius, AllAreas))
                return false;
            if (!NavMesh.SamplePosition(WorldConvert.ToUnity(to), out NavMeshHit end, PathSnapRadius, AllAreas))
                return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return false;
            Vector3[] corners = path.corners;
            if (corners.Length == 0) return false;
            for (int i = 1; i < corners.Length; i++)
                if (Planar(start.position, corners[i]) >= MinLegDistance)
                {
                    corner = WorldConvert.ToSnv(corners[i]);
                    return true;
                }
            // Every corner within a stride: the path is straight (or the character already stands there),
            // so the destination itself is the leg.
            corner = WorldConvert.ToSnv(corners[corners.Length - 1]);
            return true;
        }

        // A path corner closer than this to the start is the start's own snap jitter, not a walking
        // direction - a bearing to a point under a stride reads as noise.
        private const float MinLegDistance = 0.5f;

        /// <summary>Clamp a glide onto walkable ground: on hitting a navmesh boundary, hop the cursor across
        /// it to the ground beyond when the block is small debris or a step seam the character can still cross
        /// cheaply (see <see cref="TrySkipBoundary"/>), else stop at the boundary; with no boundary between the
        /// points, snap the target onto the mesh so the cursor never leaves the floor.</summary>
        public Snv TraceMove(Snv from, Snv intended)
        {
            Vector3 f = WorldConvert.ToUnity(from), t = WorldConvert.ToUnity(intended);
            if (NavMesh.Raycast(f, t, out NavMeshHit boundary, AllAreas))
            {
                Vector3 dir = t - f; dir.y = 0f;
                float len = dir.magnitude;
                if (len > 1e-4f)
                {
                    dir /= len;
                    // A hit AT the ray's own origin is the cursor sitting on a mesh edge (a hop's resume
                    // point, the fragmented kerb strips around the plaza roundabout), where Raycast blocks
                    // instantly though the floor continues - so try the whole step directly first, or the
                    // stroke degrades into probe-step crawling through the boundary hop below.
                    if ((boundary.position - f).sqrMagnitude < PhantomHit * PhantomHit
                        && NavMesh.SamplePosition(t, out NavMeshHit direct, ProbeRadius, AllAreas)
                        && AlignedResume(f, dir, direct.position)
                        && CheapWalk(f, direct.position))
                        return WorldConvert.ToSnv(direct.position);
                    if (TrySkipBoundary(boundary.position, dir, out Vector3 resume))
                        return WorldConvert.ToSnv(resume);
                }
                return WorldConvert.ToSnv(boundary.position);
            }
            if (NavMesh.SamplePosition(t, out NavMeshHit snapped, 1.5f, AllAreas))
                return WorldConvert.ToSnv(snapped.position);
            return intended;
        }

        /// <summary>Distance to the first navmesh boundary along a cardinal, for the wall tones: cast a
        /// navmesh ray out to <paramref name="range"/> and measure the planar gap to the hit, or report
        /// <paramref name="range"/> (no wall, silent) when the ray reaches the end unobstructed. Boundaries the
        /// cursor would hop (small debris, step seams; see <see cref="TrySkipBoundary"/>) are seen through and the cast
        /// continues beyond them, so the tone sounds only real walls and never contradicts the cursor. The
        /// cursor is navmesh-clamped, so an off-mesh origin (where Raycast would misbehave) does not arise in
        /// play.</summary>
        public float WallDistance(Snv from, Snv direction, float range)
        {
            Vector3 origin = WorldConvert.ToUnity(from);
            Vector3 dir = WorldConvert.ToUnity(direction); // unit cardinal
            Vector3 castFrom = origin;
            for (int hops = 0; hops <= MaxSeeThrough; hops++)
            {
                // Cast only the range still unspent, measured radially from the original origin so a
                // laterally-snapped resume point can't inflate the reported distance.
                float remaining = range - Planar(origin, castFrom);
                if (remaining <= 0f) return range;
                if (!NavMesh.Raycast(castFrom, castFrom + dir * remaining, out NavMeshHit hit, AllAreas))
                    return range;
                float dist = Planar(origin, hit.position);
                if (dist >= range) return range;
                if (TrySkipBoundary(hit.position, dir, out Vector3 resume)) { castFrom = resume; continue; }
                return dist;
            }
            return range; // saw through the hop cap without a real wall: treat as clear
        }

        // Can the cursor hop a navmesh boundary at <paramref name="boundary"/> travelling along unit
        // <paramref name="dir"/>? March past it up to SkipProbeDistance for where the mesh resumes, then take
        // the gap only when a complete path from the boundary to that ground exists and is no longer than
        // SkipDetourRatio times the straight hop - so small debris (a short walk-around) and stair steps
        // (whose treads connect directly) are skipped while a thin wall with ground close behind it (a long
        // walk-around, or no path at all) is not. Boundaries include STEP SEAMS, not just gaps: a staircase
        // bakes as treads the pathfinder walks but Raycast stops at (the plaza roundabout's platform stairs),
        // with the next tread a hair forward and a fraction of a metre up - so resumed ground is judged by
        // planar FORWARD PROGRESS along the glide (a near-edge snap-back has none), never by an absolute
        // gap floor, which re-seals exactly those seams. The single source of truth for "passable" shared
        // by the cursor clamp and the wall-tone cast, so they never disagree. Tuning constants below are
        // hot-reloadable (F6): a pure-module edit re-lands them live.
        private static bool TrySkipBoundary(Vector3 boundary, Vector3 dir, out Vector3 resume)
        {
            resume = default;
            for (float t = ProbeStep; t <= SkipProbeDistance; t += ProbeStep)
            {
                if (!NavMesh.SamplePosition(boundary + dir * t, out NavMeshHit r, ProbeRadius, AllAreas))
                    continue;
                if (!AlignedResume(boundary, dir, r.position))
                    continue; // near-edge or sideways snap: keep marching
                if (!CheapWalk(boundary, r.position)) return false;
                resume = r.position;
                return true;
            }
            return false;
        }

        // Whether a candidate resume point is genuinely along the stroke: planar forward progress past
        // <paramref name="origin"/> along unit <paramref name="dir"/>, deviating sideways no more than it
        // advances (a 45-degree cone). The forward floor rejects a snap-back to the near edge; the cone
        // keeps the stroke honest - on a thin mesh sliver through a void pocket, an unconstrained snap
        // projects every push onto the sliver like a bead on a wire (a west stroke sliding the cursor
        // south, proven on the road strip west of the plaza roundabout), where a stroke that cannot
        // mostly-advance should bump instead.
        private static bool AlignedResume(Vector3 origin, Vector3 dir, Vector3 candidate)
        {
            float forward = (candidate.x - origin.x) * dir.x + (candidate.z - origin.z) * dir.z;
            if (forward < ForwardEpsilon) return false;
            float lx = (candidate.x - origin.x) - dir.x * forward;
            float lz = (candidate.z - origin.z) - dir.z * forward;
            return lx * lx + lz * lz <= forward * forward;
        }

        // Whether a complete navmesh walk connects the points at no more than the hop detour allowance -
        // the "same floor, trivially crossed" test shared by the boundary hop and the phantom-edge step.
        private static bool CheapWalk(Vector3 a, Vector3 b)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a, b, AllAreas, path)) return false;
            if (path.status != NavMeshPathStatus.PathComplete) return false;
            return PathLength(path) <= Mathf.Max(Vector3.Distance(a, b) * SkipDetourRatio, MinDetourAllowance);
        }

        private static float PathLength(NavMeshPath path)
        {
            float len = 0f;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++) len += Vector3.Distance(c[i - 1], c[i]);
            return len;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whether the point sits inside the camera's visible frame, inset by <see cref="ViewMargin"/>
        /// so edge-of-frame content that streams unreliably doesn't count. The game's camera is slaved to the
        /// character every frame (nothing in the game ever unsets it and the player has no pan input), so this
        /// frame is a stable window around the body. No camera yet (early boot) reads true - not ready, not
        /// a failure.</summary>
        public bool InView(Snv point)
        {
            Camera cam = GameCamera;
            if (cam == null) return true;
            Vector3 vp = cam.WorldToViewportPoint(WorldConvert.ToUnity(point));
            return vp.z > 0f
                   && vp.x >= ViewMargin && vp.x <= 1f - ViewMargin
                   && vp.y >= ViewMargin && vp.y <= 1f - ViewMargin;
        }

        /// <summary>The nearest in-frame walkable point: clamp the point's viewport coordinates into the
        /// margin-inset frame at its own camera depth, then snap the result onto the navmesh so the cursor
        /// stays on the floor. Backs the frame-drag that keeps a pinned cursor riding the window's edge as
        /// the character walks.</summary>
        public Snv ClampToView(Snv point)
        {
            Camera cam = GameCamera;
            if (cam == null) return point;
            Vector3 vp = cam.WorldToViewportPoint(WorldConvert.ToUnity(point));
            vp.x = Mathf.Clamp(vp.x, ViewMargin, 1f - ViewMargin);
            vp.y = Mathf.Clamp(vp.y, ViewMargin, 1f - ViewMargin);
            Vector3 world = cam.ViewportToWorldPoint(vp);
            if (NavMesh.SamplePosition(world, out NavMeshHit snapped, ClampSnapRadius, AllAreas))
                return WorldConvert.ToSnv(snapped.position);
            return WorldConvert.ToSnv(world);
        }

        /// <summary>Whether the point lies under an unrevealed fog-of-war zone, by the one fog contract
        /// every sense shares (<see cref="FogSense"/>): only never-entered UNSEEN space hides - a dimmed,
        /// previously-visited room is knowable, and the cursor may glide into it exactly because the scanner
        /// and the cursor's naming offer what stands there. The capped probe also keeps the Whirling's
        /// stacked floors from shadowing each other. Zone colliders only exist in physics while their area
        /// is loaded, which is the only time such ground can be in frame; unzoned ground is never fogged.</summary>
        public bool IsFogged(Snv point)
            => FogSense.At(WorldConvert.ToUnity(point)) == FogSense.ZoneState.Unseen;

        /// <summary>Assert the camera's zoom at the area's own maximum (the widest a sighted player can see
        /// here), so the cursor's roam window is as large and as consistent as the game allows and a stray
        /// scroll-wheel tick can't shrink the senses. The limits are the game's per-area values (interior,
        /// exterior, thought-modified), read live and never stored; the setter routes through the game's own
        /// zoom-change path, so the curve and clamps apply. Called only while the world owns the keyboard, so
        /// a dialogue or cutscene zoom sequence is never fought.</summary>
        public void PinZoom()
        {
            CameraController cam = CameraController.Current;
            if (cam == null) return;
            float max = cam.GetZoomLimiters().y;
            if (Mathf.Abs(cam.ZoomFactor - max) > 0.001f) cam.ZoomFactor = max;
        }

        private static Camera GameCamera
        {
            get { CameraController cam = CameraController.Current; return cam != null ? cam._camera : null; }
        }

        // NavMesh.AllAreas (-1, every area in the mask); the const isn't surfaced on the interop proxy.
        private const int AllAreas = -1;

        // The visible-frame inset: content exactly on the frame border streams unreliably, so the cursor's
        // world ends a little inside it.
        private const float ViewMargin = 0.05f;
        // Navmesh snap radius for the frame-drag clamp - generous, since a viewport-clamped point lands at
        // the old camera depth and can float a little off the floor.
        private const float ClampSnapRadius = 2.5f;
        // Navmesh snap radius for the path-completeness test (the cursor glide's snap radius): a stored
        // point was captured on the mesh, so a small tolerance covers drift, while a large one would
        // "reach" the wrong floor of a stacked interior.
        private const float PathSnapRadius = 1.5f;
        // Cursor debris-skip tuning (see TrySkipBoundary). Chosen by profiling Martinaise's navmesh: at a ~1 m
        // gap the boundaries are still thin seams and genuinely small debris (all measuring a tight sub-1.8
        // detour), while a detour within 2x the straight hop separates that debris from a thin wall with ground
        // close behind it. Wider than this reads as a leap rather than a hop.
        private const float SkipProbeDistance = 1.0f; // widest gap (metres) the cursor will hop
        private const float SkipDetourRatio = 2.0f;   // max walk-around length as a multiple of the straight hop
        private const float ProbeStep = 0.1f;         // march resolution past the boundary
        // Snap radius when sampling for resumed ground. The probe line marches FLAT at the boundary's own
        // height while a staircase's treads rise away from it (the roundabout platform stairs sit ~0.3 m
        // above the line one tread in), so the radius must span that rise; a tighter radius walks the whole
        // march past a step seam finding nothing.
        private const float ProbeRadius = 0.5f;
        // The least planar forward progress a sample must make to count as ground BEYOND the boundary rather
        // than a snap-back to the near edge. A step seam's next tread can sit ~0.15 m out, so this stays well
        // under that; near-edge snaps measure ~0 along the glide direction.
        private const float ForwardEpsilon = 0.05f;
        // The detour allowance's floor: a step seam's straight hop is tiny (~0.15 m), and twice that would
        // refuse the stair path's own riser zigzag, so a hop this short is allowed a short absolute walk-around.
        private const float MinDetourAllowance = 0.5f;
        // A raycast hit within this of the ray's origin is the cursor standing on the mesh edge itself
        // (see TraceMove), not a boundary out along the stroke.
        private const float PhantomHit = 0.05f;
        private const int MaxSeeThrough = 3;          // wall-tone cast: most skippable boundaries to see through

        private static Character Main
        {
            get { Party party = Party.Player; return party != null ? party.Main : null; }
        }
    }
}
