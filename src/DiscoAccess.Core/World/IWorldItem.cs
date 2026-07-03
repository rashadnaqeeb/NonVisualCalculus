using System.Numerics;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The sensing-facing view of one thing in the world: what the cursor readout, sonar, and scanner read.
    /// Implemented by a thin Module proxy over a live game object, so every property reads live (the "never
    /// cache game state" rule) and no Unity type crosses into Core - the proxy converts to
    /// <see cref="System.Numerics.Vector3"/> and the Core <see cref="ScanBounds"/> at the boundary.
    /// </summary>
    public interface IWorldItem
    {
        /// <summary>A display name (a clean object name, or an orb's text). May be empty.</summary>
        string Name { get; }

        /// <summary>The thing's live world centre (where the cursor would snap).</summary>
        Vector3 Position { get; }

        /// <summary>The thing's spatial extent, for distance/bearing to its nearest part.</summary>
        ScanBounds Bounds { get; }

        /// <summary>The <see cref="WorldTaxonomy"/> category key this thing sounds and lists under.</summary>
        string Category { get; }

        /// <summary>The actionability gate: whether a sighted player with this build could act on it now
        /// (equipment, passive checks, reachability all folded in). The signal that separates the ~90
        /// actionable things from the ~400 entities of clutter.</summary>
        bool IsAccessible { get; }

        /// <summary>Whether the player could currently know about this thing (revealed, streamed in).</summary>
        bool IsVisible { get; }

        /// <summary>Whether this thing is a door standing open - visible on screen as the rotated-open
        /// panel, so it is announced and sounded distinctly. False for a closed door (the state a blind
        /// player assumes, never announced) and for anything with no open/closed state at all.</summary>
        bool IsOpen { get; }

        /// <summary>Whether this thing rides the player character rather than sitting at a fixed world spot
        /// (a thought-cabinet orb orbiting the character). Such a thing sits on top of the character, so the
        /// cursor's near-player skip - which drops the character's own entity so it is never hover-announced -
        /// must not drop it. False for everything world-anchored.</summary>
        bool RidesPlayer { get; }

        /// <summary>The spot the game's click would walk the player to in order to act on this thing:
        /// the main character's slot in the click's own priced destination formation (the cheapest
        /// reachable authored stand formation, else the game's radius-searched interaction location
        /// computed from <paramref name="from"/>). Always on the player's own walkable ground when the
        /// click would act, so the cursor can be parked there. The point the cursor-to-scanned-thing
        /// move lands on; readouts describe the thing itself (<see cref="Bounds"/>). An orb answers the
        /// walkable ground its gather walk ends on, under a body that can float far off the mesh.</summary>
        Vector3 InteractionPoint(Vector3 from);

        /// <summary>Whether acting on this thing from <paramref name="from"/> would succeed, for the
        /// discovery gates (cursor hover, scanner offer). For a person or a marker-bearing thing this is
        /// the game's own click verdict - a MovementCommand priced to the authored stand-spots, refused
        /// while every path is severed - which is anchored to the party's live position (the walk can only
        /// start at the character), so gates must pass the player as <paramref name="from"/>. A markerless
        /// thing falls back to standing-ground walk-connectivity: the ground its body stands on - or, for a
        /// body over unwalkable surface, the ground its clickable edge meets (a boat moored against a
        /// walkway) - is walk-connected to <paramref name="from"/>. Never cached and never inferred from
        /// <see cref="IsAccessible"/>, which a walled-off thing passes while unreachable; a thing
        /// unreachable from here can become reachable once the character moves.</summary>
        bool ReachableFrom(Vector3 from);

        /// <summary>Trigger the game's interaction for this thing (auto-path and act). Returns whether
        /// something was triggered.</summary>
        bool Interact();
    }
}
