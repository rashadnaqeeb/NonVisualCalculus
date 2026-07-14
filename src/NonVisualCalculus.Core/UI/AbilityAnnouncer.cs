namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused ability on the Adjust Abilities screen from its
    /// <see cref="AbilityState"/>. Order follows the house style: the ability name first (the
    /// distinguishing word, since the player moves between the four abilities), then its value and
    /// qualitative grade, then the flavor description last so a quick navigator hears the mechanical
    /// detail before the longer text is cut by the next focus. <see cref="ComposeValue"/> yields the
    /// value and grade alone, for re-announcing an in-place change (pressing plus or minus) where the
    /// name was already spoken on focus. Every word is the game's own localized text; nothing is
    /// mod-authored.
    /// </summary>
    public static class AbilityAnnouncer
    {
        public static string Compose(AbilityState s)
        {
            return Text.SpokenLine.Join(s.Name + " " + s.Value, s.Grade, s.Description);
        }

        public static string ComposeValue(AbilityState s)
        {
            return Text.SpokenLine.Join(s.Value.ToString(), s.Grade);
        }
    }
}
