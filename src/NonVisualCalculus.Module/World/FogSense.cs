using UnityEngine;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The fog-of-war sense the world proxies share: what an interior's unseen-room volume
    /// (<see cref="Sunshine.Unseen.Zone"/>, under "fow-unrevealers") says about a point. The probe is the
    /// game's own point-to-zone idiom - a raycast straight up on the fog layer into the zone's ceiling
    /// volume (Unseen.Observer and MouseOverHighlight.RegisterAndTurnMeOff both cast exactly this) - so
    /// the mod's notion of "under fog" can never drift from the game's. The game's runtime state cannot
    /// answer instead: its own fog gating of interactables (RegisterAndTurnMeOff deactivating a highlight
    /// registered to its zone) has no caller in this build, so every highlight stays live under the black
    /// and the game's pickers lean on the character's short reach rather than on fog - the mod's senses
    /// cover the whole frame, so they must ask the volumes directly.
    /// </summary>
    internal static class FogSense
    {
        internal enum ZoneState
        {
            /// <summary>No zone volume overhead: exteriors, corridors, revealed open space.</summary>
            None,
            /// <summary>Under a zone the player has entered before (ACTIVE, or INACTIVE and currently
            /// dimmed) - knowable space a sighted player reads at a glance.</summary>
            Knowable,
            /// <summary>Under a never-entered zone's volume, rendered as black void.</summary>
            Unseen,
        }

        /// <summary>The zone state above a point, read live (a zone flips ACTIVE the frame the player
        /// steps under its volume - nothing reveals a zone on a door opening; the lit sliver seen through
        /// an open door is the unseen shroud's own soft edge, not a status change).</summary>
        internal static ZoneState At(Vector3 point)
        {
            if (!Physics.Raycast(point, Vector3.up, out RaycastHit hit, ProbeDistance, FogLayerMask))
                return ZoneState.None;
            var zone = hit.collider.GetComponent<Sunshine.Unseen.Zone>();
            if (zone == null) return ZoneState.None;
            return zone.status == Sunshine.Unseen.Zone.Status.UNSEEN ? ZoneState.Unseen : ZoneState.Knowable;
        }

        /// <summary>Whether any fog volume sits within arm's reach of the point, status-blind. The volumes'
        /// meshes cover rooms' open interiors but stop at walls, so a wall-recessed body reads no zone from
        /// the up-probe at all; this is the trigger for the approach judgement on such a body (see
        /// EntityProxy.IsVisible). False means genuinely clear of every volume - exteriors, open corridors.</summary>
        internal static bool NearVolume(Vector3 point)
            => Physics.CheckBox(point + Vector3.up * (ProbeDistance / 2f),
                                new Vector3(AdjacencyReach, ProbeDistance / 2f, AdjacencyReach),
                                Quaternion.identity, FogLayerMask);

        // How far sideways NearVolume looks: wide enough to catch the volume's rim from a body recessed
        // into a room's wall (the medicine cabinet sits 0.3 m past the rim, behind mesh the volume does
        // not cover), narrow enough that most of a corridor stays clear of the rooms flanking it.
        private const float AdjacencyReach = 1f;

        // The fog-zone volumes' physics layer (the game's own zone probes cast this same mask).
        private const int FogLayerMask = 0x2000;
        // Far enough to reach this room's fog volume overhead, short of the next floor up: the Whirling's
        // co-loaded floors stack ~6 m apart in world space, so a longer cast could hit the floor above's
        // volume and hide a thing that is in plain view.
        private const float ProbeDistance = 4f;
    }
}
