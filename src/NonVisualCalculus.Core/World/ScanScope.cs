using System;
using System.Numerics;
using NonVisualCalculus.Core.World.Overlays;

namespace NonVisualCalculus.Core.World
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
    /// The reference position is the PLAYER, never the cursor: the only act a scanned thing supports is a
    /// walk-interact, and that walk starts at the character, so membership judged from a planted cursor
    /// would offer things the player's own click refuses (move the cursor, press Enter, and the character
    /// walking over is what reveals more). The sonar keeps the cursor only as its listening ear - ping
    /// placement and sweep radius - while reading this same player-anchored set.
    ///
    /// Reachability is asked per kind. A PERSON, a CROSSING (door, exit - the things that systematically
    /// sit severed behind other closed doors, which carve the walkable mesh), a CLICK-PRICED thing
    /// (<see cref="IWorldItem.ReachIsClickPriced"/> - marker-bearing, whose reach verdict is the game's own
    /// click pricing), and anything past the same-level pivot slack
    /// (<see cref="Overlays.Systems.ObjectCueSystem"/>) must pass ReachableFrom - the game's own click
    /// verdict for marker-bearing things and people (pricing to the authored stand-spots: the balcony
    /// smoker, spoken to from the street four metres below, stays offered; Cuno beyond the yard fence drops
    /// until a path opens; the corridor doors return the moment the player's own door opens; the sealed-room
    /// pinball drops though it sits on the player's own level), and the standing-ground walk-connectivity
    /// geometry for the markerless rest (the crate up the harbour gate connects via its stairs; the
    /// mezzanine door over the bar floor does not). A same-level markerless non-crossing thing is kept unless
    /// its refusal is trustworthy: a <see cref="ReachState.Severed"/> verdict (its ground was found and the
    /// path to it is cut, and the game's click would refuse too) drops it, as for the sealed backroom box;
    /// an <see cref="ReachState.Unproven"/> verdict (the standing-ground finder missed the floor) keeps it,
    /// so a genuinely over-rejected thing still pings and its walk-interact reports the wall.
    /// </summary>
    public static class ScanScope
    {
        public static bool Offered(IWorldItem it, Vector3 from, IWorldEnvironment env)
        {
            if (!it.IsAccessible || !it.IsVisible) return false;
            Vector3 nearest = it.Bounds.NearestPoint(from);
            if (!env.InView(nearest)) return false;
            // A person, a crossing, a click-priced thing, or anything off the player's level must prove it is
            // reachable - the reach verdict is trustworthy for those (see the kinds above).
            if (it.Category == WorldTaxonomy.Npc
                || it.Category == WorldTaxonomy.Door || it.Category == WorldTaxonomy.Exit
                || it.ReachIsClickPriced
                || Math.Abs(nearest.Y - from.Y) > Overlays.Systems.ObjectCueSystem.SameLevelSlack)
                return it.ReachableFrom(from) == ReachState.Reachable;
            // Same-level markerless thing: keep it unless the reach test can be TRUSTED to say no. A severed
            // path over located ground is trustworthy (the sealed backroom box drops); a ground-finder miss is
            // not (an over-rejected thing stays, and its walk-interact reports the wall).
            return it.ReachableFrom(from) != ReachState.Severed;
        }
    }
}
