using System;
using System.Numerics;

namespace DiscoAccess.Core.World
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

        /// <summary>Planar (XZ) distance in metres — height is reported separately via
        /// <see cref="VerticalSign"/>, so it does not inflate the spoken distance.</summary>
        public static float Distance(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X, dz = to.Z - from.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whether the two points coincide on the XZ plane (within <see cref="HereEpsilon"/>).</summary>
        public static bool IsHere(Vector3 from, Vector3 to)
            => Math.Abs(to.X - from.X) < HereEpsilon && Math.Abs(to.Z - from.Z) < HereEpsilon;

        /// <summary>The eight-point compass bearing from one point to another as an index 0..7
        /// (0 = north = +Z, 2 = east = +X, clockwise), or -1 when the points coincide (no bearing). The
        /// announce layer maps the index to a localized compass word.</summary>
        public static int CompassIndex(Vector3 from, Vector3 to)
        {
            if (IsHere(from, to)) return -1;
            float dx = to.X - from.X, dz = to.Z - from.Z;
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
