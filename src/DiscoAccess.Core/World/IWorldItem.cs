using System.Numerics;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// A thing's reachability verdict from a reference position. Richer than a bool because a "no" comes in
    /// two kinds the discovery gates must treat differently: one they can trust to hide the thing, one they
    /// cannot.
    /// </summary>
    public enum ReachState
    {
        /// <summary>A walk from the reference would arrive: a complete navmesh path to the stand-ground, or
        /// (a click-priced thing) the game's own click prices the approach finite, or (an orb) the walk lands
        /// inside the trigger sphere.</summary>
        Reachable,

        /// <summary>A TRUSTWORTHY refusal: the stand target was located - a click-priced thing's authored
        /// stand-spot, or a markerless thing's own standing ground - and the navmesh path to it is genuinely
        /// severed (the game's click would refuse too). Even the permissive same-level gate drops on this: the
        /// sealed backroom box, the sealed-room pinball.</summary>
        Severed,

        /// <summary>A refusal the discovery gates must NOT trust: the markerless standing-ground geometry could
        /// not locate the body's floor at all (a body hung past the drop cap, a pivot floating over its
        /// surface), or an orb's own trigger test could not confirm a reach. The refusal is the finder missing,
        /// not a proven severance, so the permissive same-level gate keeps the thing rather than hide something
        /// a sighted player can still reach (the dress shirt past the old cap); its walk-interact reports the
        /// wall if it really is blocked.</summary>
        Unproven
    }

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
        /// discovery gates (cursor hover, scanner offer), as a three-way <see cref="ReachState"/>. For a
        /// person or a marker-bearing thing this is the game's own click verdict - a MovementCommand priced to
        /// the authored stand-spots, refused while every path is severed - which is anchored to the party's
        /// live position (the walk can only start at the character), so gates must pass the player as
        /// <paramref name="from"/>. A markerless thing falls back to standing-ground walk-connectivity: the
        /// ground its body stands on - or, for a body over unwalkable surface, the ground its clickable edge
        /// meets (a boat moored against a walkway) - is walk-connected to <paramref name="from"/>. When that
        /// ground is found but severed the verdict is <see cref="ReachState.Severed"/> (trustworthy); when the
        /// finder cannot locate the floor at all it is <see cref="ReachState.Unproven"/> (untrustworthy). Never
        /// cached and never inferred from <see cref="IsAccessible"/>, which a walled-off thing passes while
        /// unreachable; a thing unreachable from here can become reachable once the character moves.</summary>
        ReachState ReachableFrom(Vector3 from);

        /// <summary>Whether <see cref="ReachableFrom"/>'s verdict here is the game's own click pricing - a
        /// person, or a thing with authored interaction stand-spots - rather than the markerless
        /// standing-ground geometry. Both verdicts are trusted when they say <see cref="ReachState.Severed"/>
        /// (the sealed-room pinball is click-priced and drops; the sealed backroom box is markerless with its
        /// ground found and its path cut, and drops too). The kinds diverge only on
        /// <see cref="ReachState.Unproven"/>: the standing-ground geometry can miss a reachable body's floor
        /// (a thing hung past the drop cap), so a markerless Unproven leaves the thing on the permissive
        /// same-level path, while the click pricing never returns Unproven - it refuses exactly when a sighted
        /// player's click would fail. This flag is off-level's companion signal (see the scanner and cursor
        /// gates), not itself an offer decision.</summary>
        bool ReachIsClickPriced { get; }

        /// <summary>Trigger the game's interaction for this thing (auto-path and act). Returns whether
        /// something was triggered.</summary>
        bool Interact();
    }
}
