namespace NonVisualCalculus.Core.Audio
{
    /// <summary>A playing positional voice whose placement can be re-set live (from the main thread) as
    /// the listener or source moves - so a one-shot keeps tracking pan/gain/ITD/filter while it's audible,
    /// instead of freezing at fire time. Updates are smoothed inside the voice so movement never clicks.</summary>
    public interface ISpatialVoice
    {
        /// <summary>Drained (clip and delay tail played out) - safe to drop from tracking.</summary>
        bool Finished { get; }

        void SetPlacement(SpatialCue cue, float volume);
    }
}
