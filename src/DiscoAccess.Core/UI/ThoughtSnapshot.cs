namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// What a thought cabinet entry is, for speech: a slot's occupancy state and, when a thought fills it
    /// (or for an item in the master list), the thought's lifecycle stage. The module adapter maps the
    /// game's own slot and thought enums onto this so the announcer's word choice stays in Core and
    /// unit-testable.
    /// </summary>
    public enum ThoughtStatusKind
    {
        Empty,        // an unlocked but unfilled slot
        Unlockable,   // a slot that can be unlocked now by spending a skill point
        Locked,       // a slot not yet unlockable
        Available,    // a thought gathered but not placed (the game's KNOWN)
        Researching,  // a thought placed and cooking; ResearchPercent is its progress
        Researched,   // a thought finished cooking and fixed
        Forgotten,    // a thought that was placed then dropped
        Unknown,      // a thought not yet discovered (its name is hidden)
    }

    /// <summary>
    /// Unity-free snapshot of one thought cabinet entry, extracted by the module adapter and composed into
    /// speech by <see cref="ThoughtAnnouncer"/>. <see cref="Name"/>, <see cref="Effects"/> and
    /// <see cref="Description"/> are the game's own localized strings (null for an empty/locked slot or an
    /// undiscovered thought, which have none); the effects are the bonuses that apply in the entry's current
    /// stage (research bonuses while cooking, completion bonuses once fixed). <see cref="ResearchPercent"/>
    /// is meaningful only for <see cref="ThoughtStatusKind.Researching"/>.
    /// </summary>
    public sealed class ThoughtSnapshot
    {
        public string? Name { get; }
        public ThoughtStatusKind Kind { get; }
        public int ResearchPercent { get; }
        public int ResearchMinutesLeft { get; }
        public int ResearchMinutesTotal { get; }
        public string? Effects { get; }
        public string? Description { get; }

        public ThoughtSnapshot(string? name, ThoughtStatusKind kind, int researchPercent = 0,
                               string? effects = null, string? description = null,
                               int researchMinutesLeft = 0, int researchMinutesTotal = 0)
        {
            Name = name;
            Kind = kind;
            ResearchPercent = researchPercent;
            Effects = effects;
            Description = description;
            ResearchMinutesLeft = researchMinutesLeft;
            ResearchMinutesTotal = researchMinutesTotal;
        }
    }
}
