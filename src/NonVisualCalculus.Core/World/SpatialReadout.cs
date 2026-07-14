using System;
using System.Numerics;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Composes the spoken line for the point under the cursor relative to the player: bearing first (the
    /// distinguishing part), then a whole-metre distance, then an above/below tag when the height differs.
    /// Mod-authored text, so it pulls every word from the strings table; engine-free and unit-tested.
    /// </summary>
    public static class SpatialReadout
    {
        public static string Describe(Vector3 reference, Vector3 cursor)
        {
            if (Geo.IsHere(reference, cursor)) return WorldHere;

            int compass = Geo.CompassIndex(reference, cursor);
            string distance = WorldDistance((int)Math.Round(Geo.Distance(reference, cursor)));
            // Directly above/below (no horizontal bearing): just distance, then the vertical tag.
            string line = compass >= 0 ? WorldCompass(compass) + ", " + distance : distance;

            int vertical = Geo.VerticalSign(reference, cursor);
            if (vertical > 0) line += ", " + WorldAbove;
            else if (vertical < 0) line += ", " + WorldBelow;
            return line;
        }
    }
}
