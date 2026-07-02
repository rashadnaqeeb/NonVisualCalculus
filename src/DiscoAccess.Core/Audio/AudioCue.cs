namespace DiscoAccess.Core.Audio
{
    /// <summary>
    /// A named one-shot cue the sensing layer can fire (the engine owns the sound file behind each name, so
    /// Core stays free of paths). The cursor's enter/exit blips as it glides across a thing's footprint -
    /// a rising click on entering, a falling click on leaving to bare ground - the impassable bump when a
    /// glide is refused at the edge of the senses, and the per-category thing cues the sonar sweep and the
    /// scanner's review ping sound (mapped from a thing's <see cref="World.WorldTaxonomy"/> category by
    /// <see cref="World.WorldCues"/>).
    /// </summary>
    public enum AudioCue
    {
        /// <summary>The cursor entered a thing's footprint (rising click).</summary>
        CursorEnter,

        /// <summary>The cursor left a thing to bare ground (falling click).</summary>
        CursorExit,

        /// <summary>A glide was refused at the edge of the senses - the visible frame's border or unrevealed
        /// fog-of-war ground. Distinct from the wall tones: a wall means "no path", this means "walk here
        /// with your body and there will be more".</summary>
        CursorImpassable,

        /// <summary>A person.</summary>
        ThingNpc,

        /// <summary>A plain interactable (and the fallback for an unmapped category).</summary>
        ThingInteractable,

        /// <summary>A container.</summary>
        ThingContainer,

        /// <summary>A skill/thought orb.</summary>
        ThingOrb,

        /// <summary>A way through - an in-place door or a destination exit.</summary>
        ThingDoor,

        /// <summary>A door standing open. Closed is the state a blind player assumes, so only open
        /// sounds different.</summary>
        ThingDoorOpen,
    }
}
