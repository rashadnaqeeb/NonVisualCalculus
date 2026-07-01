namespace DiscoAccess.Core.Audio
{
    /// <summary>
    /// A named one-shot cue the sensing layer can fire (the engine owns the sound file behind each name, so
    /// Core stays free of paths). Today: the cursor's enter/exit blips as it glides across a thing's
    /// footprint - a rising click on entering, a falling click on leaving to bare ground.
    /// </summary>
    public enum AudioCue
    {
        /// <summary>The cursor entered a thing's footprint (rising click).</summary>
        CursorEnter,

        /// <summary>The cursor left a thing to bare ground (falling click).</summary>
        CursorExit,
    }
}
