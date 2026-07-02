using DiscoAccess.Core.World;
using Vector3 = System.Numerics.Vector3;

namespace DiscoAccess.Module.World
{
    /// <summary>
    /// A world thing the Enter verb can walk to and act on: the sensing contract (<see cref="IWorldItem"/>)
    /// plus the extra facts <see cref="WalkInteract"/> needs beyond it. An entity's click is self-driving
    /// (<see cref="InteractWalks"/>), so the verb fires it and the game does the rest; an orb triggers only
    /// in place, so the verb walks the character itself through the stand-point and arrival-range members.
    /// </summary>
    internal interface IWalkTarget : IWorldItem
    {
        /// <summary>Whether <see cref="Interact(bool)"/> runs the game's own click - pricing the approach,
        /// walking the whole party, and acting on arrival - so the walk verb fires it directly instead of
        /// driving a walk first. False means the thing only acts in place (an orb) and the verb owns the
        /// walk.</summary>
        bool InteractWalks { get; }

        /// <summary>The interaction with the pace choice: <paramref name="run"/> is the double-click of the
        /// game's own click flow (run to the target). Only a self-driving target reads it; an orb triggers
        /// in place regardless.</summary>
        bool Interact(bool run);

        /// <summary>The stand-point to walk to and the heading to face on arrival, computed from
        /// <paramref name="from"/> (the character's current position).</summary>
        Vector3 Approach(Vector3 from, out float heading);

        /// <summary>Whether the character at <paramref name="playerPos"/> stands close enough to act.</summary>
        bool WithinInteractionRadius(Vector3 playerPos);

        /// <summary>The line to speak right after a successful <see cref="IWorldItem.Interact"/>, or null when
        /// nothing extra is said. An entity is silent (the game reacts on its own); a simple orb returns the
        /// clue text it floats into the world, which no other reader carries, so the mod voices it itself.</summary>
        string PostInteractLine();
    }
}
