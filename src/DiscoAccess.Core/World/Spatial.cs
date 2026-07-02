using System;
using DiscoAccess.Core.Audio;

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
        // Max interaural delay ~ head width / speed of sound ~ 0.22 m / 343 m/s ~ 0.66 ms.
        private const float MaxItdSeconds = 0.00066f;

        // Front/back cue: a high-shelf CUT deepening from transparent (due-side) to MaxShelfCutDb
        // (due-south). A shelf rather than a lowpass because the cue sounds are bright and narrowband -
        // a lowpass would silence them outright behind the listener, where a shelf makes broadband
        // sounds darker and bright sounds quieter by the same dB, so nothing ever disappears. Ramped in
        // dB (log-perceptual), which the ear hears as an even slide.
        public const float ShelfHz = 1500f;      // corner: above it sits the cue sounds' brightness
        public const float MaxShelfCutDb = -15f; // due-south: the deepest cut

        /// <summary>Stereo pan in [-1, 1] for a thing whose nearest point is <paramref name="dx"/> metres to
        /// the side (east positive) at planar distance <paramref name="dist"/>. Close in, pan tracks the
        /// lateral offset; far out it saturates toward the bearing. <paramref name="panWidth"/> is the
        /// crossover distance. Coincident (dist ~ 0) reads centred.</summary>
        public static float Pan(float dx, float dist, float panWidth)
            => dist > 1e-3f ? WorldMath.Clamp(dx / Math.Max(dist, panWidth), -1f, 1f) : 0f;

        /// <summary>Full direction cues for a thing offset <paramref name="dx"/> metres east and
        /// <paramref name="dz"/> metres north of the listener (the WOTR spatializer model, on the top-down
        /// XZ plane the compass readout uses). East/west becomes the constant-power <see cref="Pan"/> plus
        /// an interaural time difference sharing the pan's lateral fraction, so the time and level cues
        /// move together; north/south becomes timbre - stereo cannot pan front/back, so sources behind the
        /// listener (south) get a progressively deeper high-shelf cut, ramping from transparent at the
        /// due-side line to <see cref="MaxShelfCutDb"/> at due-south (muffled = behind, bright = ahead,
        /// the audiogame convention). Distance stays the caller's volume job.</summary>
        public static SpatialCue Cue(float dx, float dz, float panWidth)
        {
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            float lat = Pan(dx, dist, panWidth);
            var cue = new SpatialCue
            {
                Pan = lat,
                ItdSeconds = MaxItdSeconds * lat,
                RearShelfHz = ShelfHz,
            };

            // Only the rear hemisphere is processed (south of the listener exactly), ramping from
            // transparent at the due-side line to the full cut at due-south.
            if (dist > 1e-3f)
            {
                float northFrac = WorldMath.Clamp(dz / dist, -1f, 1f); // +1 ahead .. -1 behind
                if (northFrac < 0f) cue.RearShelfDb = MaxShelfCutDb * -northFrac;
            }
            return cue;
        }

        /// <summary>Volume in [<paramref name="floor"/>, 1] falling with distance on the curve
        /// refDist / (refDist + dist): full at the thing, half a reference-distance away, never below the
        /// floor so a far-but-revealed thing stays faintly audible. The caller's own volume setting scales
        /// this on top.</summary>
        public static float DistanceVolume(float dist, float refDist, float floor)
            => WorldMath.Clamp(refDist / (refDist + dist), floor, 1f);

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
            => WorldMath.Clamp(spread / Math.Max(1, count), gapMin, gapMax);
    }
}
