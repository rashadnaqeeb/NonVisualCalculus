using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused skill on the signature skill screen from its
    /// <see cref="SkillState"/>. Order follows the house style: the skill name first (the distinguishing
    /// word, since the player moves across the grid), then its value, then the signature marker when this
    /// skill is the chosen one, then a "can raise" marker on the in-game sheet when a point can be spent
    /// here, then the flavor description, and finally the in-game sheet's info-panel detail (the bonus
    /// breakdown then the long encyclopedic description) folded on last so a quick navigator hears the
    /// mechanical detail before the longer text is cut by the next focus. <see cref="ComposeSignature"/> yields the signature marker
    /// alone (empty when not the signature), for re-announcing the moment the player sets this focused
    /// skill as their signature, where the name was already spoken on focus. The name and description are
    /// the game's own localized text; the signature and "can raise" markers are mod-authored.
    /// </summary>
    public static class SkillAnnouncer
    {
        public static string Compose(SkillState s)
        {
            return Text.SpokenLine.Join(s.Name + " " + s.Value, SignatureWord(s), RaiseWord(s), s.Description,
                s.Bonuses, s.LongDescription);
        }

        public static string ComposeSignature(SkillState s)
        {
            return SignatureWord(s);
        }

        /// <summary>The skill's value and its markers, for re-announcing after a skill point is spent here:
        /// the raised value first, then the "can raise" marker if a point still remains, then the signature
        /// marker. The name was already spoken on focus, so it is omitted.</summary>
        public static string ComposeLeveled(SkillState s)
        {
            return Text.SpokenLine.Join(s.Value.ToString(), RaiseWord(s), SignatureWord(s));
        }

        private static string SignatureWord(SkillState s)
        {
            return s.IsSignature ? StatusSignature : string.Empty;
        }

        private static string RaiseWord(SkillState s)
        {
            return s.CanRaise ? StatusCanRaise : string.Empty;
        }
    }
}
