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

        /// <summary>The player controls the character when one exists and no conversation is up. The world
        /// reader already only ticks on the in-game (LOBBY) view, so this is the finer cutscene/dialogue
        /// gate on top of that.</summary>
        public bool HasControl => Main != null && !DialogueManager.isConversationActive;

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

        /// <summary>Clamp a glide onto walkable ground: on hitting a navmesh boundary, hop the cursor across
        /// it to the ground beyond when the block is small debris the character can still round cheaply (see
        /// <see cref="TrySkipBoundary"/>), else stop at the boundary; with no boundary between the points, snap
        /// the target onto the mesh so the cursor never leaves the floor.</summary>
        public Snv TraceMove(Snv from, Snv intended)
        {
            Vector3 f = WorldConvert.ToUnity(from), t = WorldConvert.ToUnity(intended);
            if (NavMesh.Raycast(f, t, out NavMeshHit boundary, AllAreas))
            {
                Vector3 dir = t - f; dir.y = 0f;
                float len = dir.magnitude;
                if (len > 1e-4f && TrySkipBoundary(boundary.position, dir / len, out Vector3 resume))
                    return WorldConvert.ToSnv(resume);
                return WorldConvert.ToSnv(boundary.position);
            }
            if (NavMesh.SamplePosition(t, out NavMeshHit snapped, 1.5f, AllAreas))
                return WorldConvert.ToSnv(snapped.position);
            return intended;
        }

        /// <summary>Distance to the first navmesh boundary along a cardinal, for the wall tones: cast a
        /// navmesh ray out to <paramref name="range"/> and measure the planar gap to the hit, or report
        /// <paramref name="range"/> (no wall, silent) when the ray reaches the end unobstructed. Boundaries the
        /// cursor would hop (small debris; see <see cref="TrySkipBoundary"/>) are seen through and the cast
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
        // SkipDetourRatio times the straight hop - so small debris (a short walk-around) is skipped while a
        // thin wall with ground close behind it (a long walk-around, or no path at all) is not. The single
        // source of truth for "passable" shared by the cursor clamp and the wall-tone cast, so they never
        // disagree. Tuning constants below are hot-reloadable (F6): a pure-module edit re-lands them live.
        private static bool TrySkipBoundary(Vector3 boundary, Vector3 dir, out Vector3 resume)
        {
            resume = default;
            for (float t = ProbeStep; t <= SkipProbeDistance; t += ProbeStep)
            {
                if (!NavMesh.SamplePosition(boundary + dir * t, out NavMeshHit r, ProbeRadius, AllAreas))
                    continue;
                float gap = Vector3.Distance(boundary, r.position);
                if (gap <= MinGap) continue; // still snapping to the near edge: keep marching past the gap
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(boundary, r.position, AllAreas, path)) return false;
                if (path.status != NavMeshPathStatus.PathComplete) return false;
                if (PathLength(path) > gap * SkipDetourRatio) return false;
                resume = r.position;
                return true;
            }
            return false;
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
        private const float ProbeRadius = 0.2f;       // snap radius when sampling for resumed ground
        private const float MinGap = 0.25f;           // ignore resume points this close to the boundary (near edge)
        private const int MaxSeeThrough = 3;          // wall-tone cast: most skippable boundaries to see through

        private static Character Main
        {
            get { Party party = Party.Player; return party != null ? party.Main : null; }
        }
    }
}
