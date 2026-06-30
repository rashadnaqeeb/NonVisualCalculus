using System;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// The pure audio-placement formulas behind the spatial soundscape, factored out of any engine so they
    /// can be unit-tested and tuned in one place. A sensing system computes the geometry (the offset and
    /// distance to the nearest part of a thing) and asks this for the stereo pan and volume, or for the next
    /// sonar sweep gap; the audio engine just plays what it is handed. Ported from the WOTR exploration mod,
    /// with distances in metres (Disco's 1 unit = 1 metre scale).
    /// </summary>
    public static class Spatial
    {
        /// <summary>Stereo pan in [-1, 1] for a thing whose nearest point is <paramref name="dx"/> metres to
        /// the side (east positive) at planar distance <paramref name="dist"/>. Close in, pan tracks the
        /// lateral offset; far out it saturates toward the bearing. <paramref name="panWidth"/> is the
        /// crossover distance. Coincident (dist ~ 0) reads centred.</summary>
        public static float Pan(float dx, float dist, float panWidth)
            => dist > 1e-3f ? Clamp(dx / Math.Max(dist, panWidth), -1f, 1f) : 0f;

        /// <summary>Volume in [<paramref name="floor"/>, 1] falling with distance on the curve
        /// refDist / (refDist + dist): full at the thing, half a reference-distance away, never below the
        /// floor so a far-but-revealed thing stays faintly audible. The per-system and master volumes scale
        /// this on top.</summary>
        public static float DistanceVolume(float dist, float refDist, float floor)
            => Clamp(refDist / (refDist + dist), floor, 1f);

        /// <summary>Wall-tone proximity volume in [0, 1]: 0 at or beyond <paramref name="range"/>, rising
        /// quadratically to 1 right at the wall, so it bites close in and stays quiet at the edge of
        /// range.</summary>
        public static float ProximityVolume(float dist, float range)
        {
            if (dist >= range || range <= 0f) return 0f;
            float t = 1f - dist / range;
            return t * t;
        }

        /// <summary>Seconds between sonar pings for a sweep of <paramref name="count"/> things:
        /// spread / count, clamped to [<paramref name="gapMin"/>, <paramref name="gapMax"/>], so a few feel
        /// spacious and a crowd compresses toward the floor (the whole sweep lengthens, nothing is
        /// dropped).</summary>
        public static float SweepGap(int count, float spread, float gapMin, float gapMax)
            => Clamp(spread / Math.Max(1, count), gapMin, gapMax);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
