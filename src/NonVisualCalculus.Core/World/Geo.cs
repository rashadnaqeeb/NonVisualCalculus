using System;
using System.Numerics;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Spatial readout math for the isometric scene, on plain <see cref="Vector3"/> world points. The
    /// convention throughout the world layer: XZ is the ground plane (Y up), one world unit is one metre
    /// (Disco's scale, confirmed in the scouting notes), so distances are reported in metres directly with
    /// no conversion. This computes raw values (a compass index, a metre distance, a vertical sign); turning
    /// them into spoken words is the announce layer's job, so this stays free of any string table and is
    /// unit-testable in isolation.
    /// </summary>
    public static class Geo
    {
        /// <summary>Two points coincide on the XZ plane within this many metres (the "here" case).</summary>
        public const float HereEpsilon = 0.05f;

        /// <summary>Vertical separation past which a thing reads as above/below (the game's own height
        /// threshold), so a thing a step up doesn't get an "above".</summary>
        public const float VerticalThreshold = 1.5f;

        /// <summary>Straight-line (3D) distance in metres. Elevation within an area is real (the city has
        /// height, not only the Whirling's separate floors), so a thing up on a ledge reads as genuinely
        /// farther rather than only being tagged "above"; the bearing stays planar and
        /// <see cref="VerticalSign"/> still gives the vertical direction.</summary>
        public static float Distance(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y, dz = to.Z - from.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Planar (XZ) distance in metres, ignoring elevation. The cursor's flat map sense: whether
        /// the cursor is over a thing's footprint is an XZ question, so a thing whose geometry sits up high
        /// (a staircase, an exit whose trigger origin floats above the steps) is still "under" the cursor
        /// gliding beneath it. Height stays out of this decision; the game's own accessibility gate and the
        /// navmesh clamp (which keeps the cursor off junk-parked, off-map entities) do the filtering.</summary>
        public static float DistanceXZ(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whether the two points coincide in all three axes — you're on it. Vertical separation
        /// past the <see cref="VerticalThreshold"/> counts, so a thing directly overhead is not "here"; it
        /// reads its height as distance plus "above".</summary>
        public static bool IsHere(Vector3 from, Vector3 to)
            => Math.Abs(to.X - from.X) < HereEpsilon
            && Math.Abs(to.Z - from.Z) < HereEpsilon
            && Math.Abs(to.Y - from.Y) < VerticalThreshold;

        /// <summary>The eight-point compass bearing from one point to another as an index 0..7
        /// (0 = north = +Z, 2 = east = +X, clockwise), or -1 when there is no horizontal bearing (the points
        /// coincide on the XZ plane, e.g. a thing directly above or below). The announce layer maps the index
        /// to a localized compass word.</summary>
        public static int CompassIndex(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            if (Math.Abs(dx) < HereEpsilon && Math.Abs(dz) < HereEpsilon) return -1; // no horizontal bearing
            double deg = Math.Atan2(dx, dz) * (180.0 / Math.PI); // 0 = +Z (north), 90 = +X (east)
            if (deg < 0) deg += 360.0;
            return (int)Math.Round(deg / 45.0) % 8;
        }

        /// <summary>+1 when <paramref name="to"/> is above <paramref name="from"/> past
        /// <see cref="VerticalThreshold"/>, -1 when below, 0 when level — the spoken "above"/"below".</summary>
        public static int VerticalSign(Vector3 from, Vector3 to)
        {
            float dy = to.Y - from.Y;
            if (dy > VerticalThreshold) return 1;
            if (dy < -VerticalThreshold) return -1;
            return 0;
        }
    }
}
