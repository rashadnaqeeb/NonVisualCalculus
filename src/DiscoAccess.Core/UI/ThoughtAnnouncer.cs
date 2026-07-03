using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused thought cabinet entry from its <see cref="ThoughtSnapshot"/>.
    /// House order: the name first (the distinguishing word, since the player moves across the grid and
    /// list), then the status word, then the entry's current effects, then its description last so a quick
    /// navigator hears the name and stage before the longer text is cut by the next focus. An empty or
    /// locked slot and an undiscovered thought have no name, so they read as the status word alone. The
    /// name, effects and description are the game's own localized text; the status words are mod-authored.
    /// </summary>
    public static class ThoughtAnnouncer
    {
        public static string Compose(ThoughtSnapshot t)
            => Text.SpokenLine.Join(t.Name, Status(t), t.Effects, t.Description);

        /// <summary>Just the status word (with research percent), for re-announcing after an Enter changes a
        /// slot or thought in place - the name was already spoken on focus.</summary>
        public static string ComposeStatus(ThoughtSnapshot t) => Status(t);

        private static string Status(ThoughtSnapshot t)
        {
            switch (t.Kind)
            {
                case ThoughtStatusKind.Empty: return ThoughtSlotEmpty;
                case ThoughtStatusKind.Unlockable: return ThoughtSlotUnlockable;
                case ThoughtStatusKind.Locked: return ThoughtSlotLocked;
                case ThoughtStatusKind.Available: return WithTotalTime(ThoughtAvailable, t);
                case ThoughtStatusKind.Researching: return WithTimeLeft(ThoughtResearching(t.ResearchPercent), t);
                case ThoughtStatusKind.Researched: return ThoughtResearched;
                case ThoughtStatusKind.Forgotten: return ThoughtForgotten;
                case ThoughtStatusKind.Unknown: return ThoughtUnknown;
                default: return string.Empty;
            }
        }

        // A cooking thought appends the in-game time left ("researching, 60 percent, 2 hours remaining").
        private static string WithTimeLeft(string status, ThoughtSnapshot t)
            => t.ResearchMinutesLeft > 0 ? status + ", " + Duration(t.ResearchMinutesLeft) + " " + ThoughtTimeRemaining : status;

        // An unplaced thought appends how long it will take to research ("available, research time 3 hours").
        private static string WithTotalTime(string status, ThoughtSnapshot t)
            => t.ResearchMinutesTotal > 0 ? status + ", " + ThoughtResearchTime + " " + Duration(t.ResearchMinutesTotal) : status;
    }
}
