using System.Numerics;

namespace DiscoAccess.Core.World.Overlays
{
    /// <summary>
    /// The engine seam the overlay framework reads to place and move the cursor, implemented by a thin
    /// Module adapter over the live game (player position, navmesh, control state). Kept an interface so the
    /// framework stays in Core (no Unity reference) and unit-testable with a fake. The adapter converts
    /// Unity's <c>Vector3</c> to <see cref="System.Numerics.Vector3"/> at this boundary.
    /// </summary>
    public interface IWorldEnvironment
    {
        /// <summary>The reference character's live world position — the origin for bearing/distance and the
        /// target of a recenter.</summary>
        Vector3 PlayerPosition { get; }

        /// <summary>Whether the player actually controls the character right now (false during a cutscene or
        /// scripted scene), so the cursor can't drift and the sensing systems can stand down.</summary>
        bool HasControl { get; }

        /// <summary>Clamp a glide from <paramref name="from"/> toward <paramref name="intended"/> onto
        /// walkable ground: returns the intended point snapped/short-stopped to the navmesh so the cursor
        /// can't leave the floor. The Module backs this with the game's navmesh queries.</summary>
        Vector3 TraceMove(Vector3 from, Vector3 intended);

        /// <summary>Planar (XZ) distance in metres from <paramref name="from"/> to the first navmesh boundary
        /// along the unit <paramref name="direction"/>, capped at <paramref name="range"/> (returns
        /// <paramref name="range"/> when no wall stands within range). Backs the wall-tone proximity
        /// volume; the Module casts the game's navmesh.</summary>
        float WallDistance(Vector3 from, Vector3 direction, float range);

        /// <summary>Hold the game camera on <paramref name="point"/> so the orb streamer (which culls against
        /// the camera frustum) wakes the orbs around it, and so an orb the cursor sits on is rendered - the
        /// gate its clickable needs before it can be interacted with. The Module takes a camera lock (which
        /// freezes the game's own camera, so it stops pulling the view back to the character between our
        /// focuses) and snaps the focus rather than tweening, so the frustum updates promptly. A no-op before
        /// the camera exists (early boot), which is a not-ready state rather than a failure.</summary>
        void FocusCamera(Vector3 point);

        /// <summary>Release the camera lock taken by <see cref="FocusCamera"/>, handing the camera back to the
        /// game - which recenters on the character. Called when control is lost, a menu floats over the world,
        /// or the world is left, so the game's own dialogue and cutscene cameras are unopposed. Idempotent.</summary>
        void ReleaseCamera();
    }
}
