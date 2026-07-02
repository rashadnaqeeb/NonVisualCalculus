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

        /// <summary>Whether this thing rides the player character rather than sitting at a fixed world spot
        /// (a thought-cabinet orb orbiting the character). Such a thing sits on top of the character, so the
        /// cursor's near-player skip - which drops the character's own entity so it is never hover-announced -
        /// must not drop it. False for everything world-anchored.</summary>
        bool RidesPlayer { get; }

        /// <summary>The on-navmesh stand-point to approach this thing from <paramref name="from"/> in order
        /// to act on it (the game's interaction location). The point the sonar pings, the scanner targets,
        /// and the go-here distance measures to: it sits on reachable ground, so following it leads to a
        /// successful interaction, and it dissolves the across-a-barrier case (a fenced NPC's talk-spot, not
        /// its unreachable body). Approach-relative, so it is recomputed from the querying position rather
        /// than a fixed landmark. A thing with no interaction stand-point (an orb, whose interaction is
        /// deferred until camera-follow) returns its body <see cref="Position"/>.</summary>
        Vector3 InteractionPoint(Vector3 from);

        /// <summary>Whether the game can currently path to this thing's interaction stand-point from
        /// <paramref name="from"/> (the game's own reachability oracle). The live per-position verdict on
        /// whether acting on it would succeed; never cached and never inferred from <see cref="IsAccessible"/>,
        /// which a walled-off thing can pass while being unreachable. A thing unreachable from here can become
        /// reachable once the character has moved.</summary>
        bool IsActionable(Vector3 from);

        /// <summary>Whether this thing genuinely belongs to the ground reachable from <paramref name="from"/>,
        /// for the discovery gates (cursor hover, scanner offer) when it sits off the reference's level: the
        /// walkable ground its body stands on - or, for a body over unwalkable surface, the ground its
        /// clickable edge meets (a boat moored on water against a walkway) - is walk-connected to
        /// <paramref name="from"/> (the crate up on the harbour gate connects via its stairs; the plaza below
        /// the Whirling balcony is another island), or it is a person the game paths a conversation to across
        /// levels (the smoker on the balcony, spoken to from the street below). Deliberately NOT
        /// <see cref="IsActionable"/>: the game's
        /// stand-point search is a 3D radius that can hand back a spot on an unrelated level over a thing's
        /// head, calling a ground-floor door "reachable" from the balcony above it.</summary>
        bool ReachableFrom(Vector3 from);

        /// <summary>Trigger the game's interaction for this thing (auto-path and act). Returns whether
        /// something was triggered.</summary>
        bool Interact();
    }
}
