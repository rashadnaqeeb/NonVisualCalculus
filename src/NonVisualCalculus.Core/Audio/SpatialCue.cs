namespace NonVisualCalculus.Core.Audio
{
    /// <summary>
    /// The stereo placement for one positional sound, computed by the sensing layer (via
    /// <see cref="NonVisualCalculus.Core.World.Spatial.Cue"/>) and handed to the engine with the cue to play.
    /// Three perceptual channels, the WOTR exploration mod's model: pan + interaural time difference for
    /// east/west, a rear high-shelf cut for north/south; distance stays a caller-owned volume.
    /// Times are seconds, not samples, so Core stays free of the engine's rate.
    /// </summary>
    public struct SpatialCue
    {
        /// <summary>-1 (hard left/west) .. +1 (hard right/east), constant-power.</summary>
        public float Pan;

        /// <summary>Interaural delay in seconds; sign = +east / -west (the far ear is delayed). Below
        /// ~1.5 kHz this is the ear's dominant left/right cue, resolved far finer than the level
        /// difference alone, so it sharpens and externalises the pan - especially on headphones.</summary>
        public float ItdSeconds;

        /// <summary>The rear cue's high-shelf corner in Hz: content above it is cut by
        /// <see cref="RearShelfDb"/>.</summary>
        public float RearShelfHz;

        /// <summary>The shelf's gain in dB, 0 (ahead/side, transparent) falling negative behind the
        /// listener. A shelf CUT, not a lowpass: broadband sounds darken and bright, narrowband cues
        /// (which a lowpass would erase) simply get quieter - behind reads as darker, never silent.</summary>
        public float RearShelfDb;
    }
}
