using System;
using System.Numerics;
using DiscoAccess.Core.World.Overlays;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The one offering gate the review senses share: the scanner's browse list (and its category counts)
    /// and the sonar's sweep read exactly this set, so what pings is always what can be browsed (the WOTR
    /// rule - the sonar and the review cycles share a single detectability test). The set is what a sighted
    /// player could see and act on right now: accessible and visible, inside the camera's visible frame.
    ///
    /// In-frame is tested at the thing's part nearest the reference, so a wide thing that pokes into the
    /// frame (a doorway half in view) still counts. Fog of war is IsVisible's contract - the item judges a
    /// fogged body at its approach stand-point, so a closed room's own door is offered from the corridor
    /// side - and this gate takes no second fog opinion: a body-position fog test here would re-hide exactly
    /// those boundary things.
    ///
    /// The height-reachability pair is the cursor's own gate (<see cref="Overlays.Systems.ObjectCueSystem"/>):
    /// a thing past the same-level pivot slack is offered only when it belongs to ground walk-connected from
    /// here (ReachableFrom) - so the crate up on the harbour gate (connected via its stairs) stays offered,
    /// while the ground-floor door and the tracks on the plaza below the balcony, reachable only by going
    /// elsewhere, never land in the set to fail a walk-interact later. A PERSON is gated by ReachableFrom at
    /// ANY height: their verdict is the game's own click pricing, which sees talkability the geometry
    /// cannot - the balcony smoker (spoken to from the street four metres below) stays offered, while a
    /// person the click refuses on the player's own level (Cuno beyond the yard fence) drops out until a
    /// path opens. The path tests run only for the few off-slack candidates and people in frame.
    ///
    /// A same-level CROSSING (door, exit) must additionally have a complete walk to its stand-point: a
    /// closed door carves the walkable mesh, so the corridor doors beyond the player's own shut door are
    /// severed and a walk-interact at them would stall against it - they return the moment it opens (the
    /// set rebuilds per press/sweep). Only crossings, because doors are the things that systematically sit
    /// behind other doors; a blanket walk requirement is the known over-rejection trap (the bartender
    /// behind his counter island, the container on a mesh-carving table) and was dropped for cost - a
    /// few doors per query keep the stand-point and path calls negligible at any sonar cadence.
    /// </summary>
    public static class ScanScope
    {
        public static bool Offered(IWorldItem it, Vector3 from, IWorldEnvironment env)
        {
            if (!it.IsAccessible || !it.IsVisible) return false;
            Vector3 nearest = it.Bounds.NearestPoint(from);
            if (!env.InView(nearest)) return false;
            if (it.Category == WorldTaxonomy.Npc
                || Math.Abs(nearest.Y - from.Y) > Overlays.Systems.ObjectCueSystem.SameLevelSlack)
                return it.ReachableFrom(from);
            if (it.Category != WorldTaxonomy.Door && it.Category != WorldTaxonomy.Exit) return true;
            return env.WalkExists(from, it.InteractionPoint(from));
        }
    }
}
