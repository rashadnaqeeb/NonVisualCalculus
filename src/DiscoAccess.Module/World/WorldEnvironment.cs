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
    /// position, whether the player has control, and the navmesh clamp through here. This is the thin
    /// engine-touching adapter the Core world layer is kept free of; it converts Unity's <c>Vector3</c> to
    /// <see cref="System.Numerics.Vector3"/> at the boundary so no Unity type crosses into Core.
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

        /// <summary>Clamp a glide onto walkable ground: stop at the first navmesh boundary between the points,
        /// else snap the target onto the mesh so the cursor never leaves the floor.</summary>
        public Snv TraceMove(Snv from, Snv intended)
        {
            Vector3 f = WorldConvert.ToUnity(from), t = WorldConvert.ToUnity(intended);
            if (NavMesh.Raycast(f, t, out NavMeshHit boundary, AllAreas))
                return WorldConvert.ToSnv(boundary.position);
            if (NavMesh.SamplePosition(t, out NavMeshHit snapped, 1.5f, AllAreas))
                return WorldConvert.ToSnv(snapped.position);
            return intended;
        }

        /// <summary>Distance to the first navmesh boundary along a cardinal, for the wall tones: cast a
        /// navmesh ray out to <paramref name="range"/> and measure the planar gap to the hit, or report
        /// <paramref name="range"/> (no wall, silent) when the ray reaches the end unobstructed. The cursor is
        /// navmesh-clamped, so an off-mesh origin (where Raycast would misbehave) does not arise in play.</summary>
        public float WallDistance(Snv from, Snv direction, float range)
        {
            Vector3 f = WorldConvert.ToUnity(from);
            Vector3 t = WorldConvert.ToUnity(from + direction * range);
            if (!NavMesh.Raycast(f, t, out NavMeshHit hit, AllAreas))
                return range;
            float dx = hit.position.x - f.x, dz = hit.position.z - f.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // A stable lock token. While it sits in the camera's lock set the game's own camera logic - pan, zoom,
        // and the recenter-on-character that otherwise pulls the view back between our focuses - is frozen, so
        // our SetFocus is the only thing moving the camera. Confirmed live: SetFocus still drives the camera
        // while locked. One per environment instance; released on leaving the world (and on module teardown,
        // via the overlay's exit), so it never leaks a frozen camera.
        private readonly Il2CppSystem.Object _camLock = new Il2CppSystem.Object();

        /// <summary>Hold the camera on a world point so the orb streamer wakes the orbs around it. Takes the
        /// camera lock once (re-added if the controller was swapped on an area change, checked live) so the
        /// game stops reclaiming the view, then snaps the focus with instant=true so the frustum updates this
        /// frame rather than tweening; the empty zoom keeps the current zoom. A no-op before the camera exists
        /// (early boot) - a not-ready state, not a failure, so it is silent like the player-position cold read.</summary>
        public void FocusCamera(Snv point)
        {
            CameraController cam = CameraController.Current;
            if (cam == null) return;
            if (!cam.CheckLock(_camLock)) cam.AddLock(_camLock);
            cam.SetFocus(WorldConvert.ToUnity(point), new Il2CppSystem.Nullable<float>(), true);
        }

        /// <summary>Release the camera lock, handing the camera back to the game (which recenters on the
        /// character). Idempotent: only removes the lock when this controller actually holds it.</summary>
        public void ReleaseCamera()
        {
            CameraController cam = CameraController.Current;
            if (cam != null && cam.CheckLock(_camLock)) cam.RemoveLock(_camLock);
        }

        // NavMesh.AllAreas (-1, every area in the mask); the const isn't surfaced on the interop proxy.
        private const int AllAreas = -1;

        private static Character Main
        {
            get { Party party = Party.Player; return party != null ? party.Main : null; }
        }
    }
}
