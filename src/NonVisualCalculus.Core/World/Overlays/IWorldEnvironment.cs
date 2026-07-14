using System.Numerics;

namespace NonVisualCalculus.Core.World.Overlays
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

        /// <summary>Whether <paramref name="point"/> sits inside the camera's visible frame (with a small
        /// inset margin, so edge-of-frame content that streams unreliably doesn't count). The frame is the
        /// cursor's roam bound: the game's camera stays slaved to the character, orbs stream against its
        /// frustum, so in-frame is exactly "rendered, revealed, and actable". Reads true before the camera
        /// exists (early boot) - a not-ready state, not a failure.</summary>
        bool InView(Vector3 point);

        /// <summary>The nearest in-frame walkable point to <paramref name="point"/>, for pulling a cursor
        /// back inside the view after the frame moved out from under it (the character walked). Returns the
        /// point unchanged before the camera exists.</summary>
        Vector3 ClampToView(Vector3 point);

        /// <summary>Whether <paramref name="point"/> lies under an unrevealed fog-of-war zone - ground a
        /// sighted player currently sees only as the dark mosaic. The Module raycasts up into the game's
        /// zone volumes, the game's own point-to-zone idiom; zones only exist in physics while their area
        /// is loaded, which is the only time such ground can be in frame.</summary>
        bool IsFogged(Vector3 point);
    }
}
