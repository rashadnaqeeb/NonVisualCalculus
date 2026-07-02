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
    /// The reference position is the PLAYER, never the cursor: the only act a scanned thing supports is a
    /// walk-interact, and that walk starts at the character, so membership judged from a planted cursor
    /// would offer things the player's own click refuses (move the cursor, press Enter, and the character
    /// walking over is what reveals more). The sonar keeps the cursor only as its listening ear - ping
    /// placement and sweep radius - while reading this same player-anchored set.
    ///
    /// Reachability is asked per kind. A PERSON, a CROSSING (door, exit - the things that systematically
    /// sit severed behind other closed doors, which carve the walkable mesh), and anything past the
    /// same-level pivot slack (<see cref="Overlays.Systems.ObjectCueSystem"/>) must pass ReachableFrom -
    /// the game's own click verdict for marker-bearing things and people (pricing to the authored
    /// stand-spots: the balcony smoker, spoken to from the street four metres below, stays offered; Cuno
    /// beyond the yard fence drops until a path opens; the corridor doors return the moment the player's
    /// own door opens), and the standing-ground walk-connectivity geometry for the markerless rest (the
    /// crate up the harbour gate connects via its stairs; the mezzanine door over the bar floor does not).
    /// A same-level non-crossing thing is offered without a path test: a walled-off woodpile still pings,
    /// and its walk-interact reports the wall - the known over-rejection traps live here, so the gate
    /// stays permissive.
    /// </summary>
    public static class ScanScope
    {
        public static bool Offered(IWorldItem it, Vector3 from, IWorldEnvironment env)
        {
            if (!it.IsAccessible || !it.IsVisible) return false;
            Vector3 nearest = it.Bounds.NearestPoint(from);
            if (!env.InView(nearest)) return false;
            if (it.Category == WorldTaxonomy.Npc
                || it.Category == WorldTaxonomy.Door || it.Category == WorldTaxonomy.Exit
                || Math.Abs(nearest.Y - from.Y) > Overlays.Systems.ObjectCueSystem.SameLevelSlack)
                return it.ReachableFrom(from);
            return true;
        }
    }
}
