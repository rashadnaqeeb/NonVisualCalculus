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
    }
}
