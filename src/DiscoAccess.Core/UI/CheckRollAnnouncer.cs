using System.Text;
using DiscoAccess.Core.Strings;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the silent roll line placed in the transcript above a resolved check's outcome line. The
    /// game's outcome line already speaks the skill, difficulty and success/failure, so this exposes only
    /// what it omits: the result against the base target, then the dice, the skill, and each modifier as a
    /// running sum. Modifiers adjust the target, so their effect on the player is the negation of their
    /// raw bonus (a target rise reads "minus N"); folding that onto the roll side lets the chain add up to
    /// the headline total against the base target. Reads "<total>/<target>: rolled <d1> plus <d2>, plus
    /// <skill> <name>, (plus|minus) <n> <modifier>", ending with the game's critical word on
    /// double six or double ones - a critical overrides the arithmetic, so without it a numeric miss
    /// that succeeded reads as a contradiction. A passive check has no dice and no chain worth walking,
    /// so it reads just the bare headline "<total>/<target>". Only the connectives are authored; the
    /// skill, modifier, and critical words are game text.
    /// </summary>
    public static class CheckRollAnnouncer
    {
        // The flat value the game folds into a passive check's displayed roll in place of dice (its check
        // tooltip's Roll line does the same), so the spoken total matches what a sighted player sees.
        private const int PassiveBase = 6;

        public static string Compose(CheckRollState s)
        {
            int modSum = 0;
            for (int i = 0; i < s.Modifiers.Count; i++)
                modSum += s.Modifiers[i].Bonus;
            int total = s.Die1 + s.Die2 + s.SkillValue - modSum;

            if (s.Passive)
                return (total + PassiveBase) + "/" + s.BaseTarget;

            var sb = new StringBuilder();
            sb.Append(total).Append('/').Append(s.BaseTarget).Append(": ");
            sb.Append(Strings.Strings.CheckRolled).Append(' ')
              .Append(s.Die1).Append(' ').Append(Strings.Strings.CheckPlus).Append(' ').Append(s.Die2);
            sb.Append(", ").Append(Strings.Strings.CheckPlus).Append(' ').Append(s.SkillValue);
            // The skill, modifier, and critical words are game text and can arrive display-shaped;
            // each inverts within its own position before the authored connectives compose around it.
            if (!string.IsNullOrEmpty(s.SkillName))
                sb.Append(' ').Append(Text.RtlText.Unfix(s.SkillName));
            for (int i = 0; i < s.Modifiers.Count; i++)
            {
                CheckRollModifier m = s.Modifiers[i];
                int effect = -m.Bonus;
                string word = effect >= 0 ? Strings.Strings.CheckPlus : Strings.Strings.CheckMinus;
                int magnitude = effect < 0 ? -effect : effect;
                sb.Append(", ").Append(word).Append(' ').Append(magnitude).Append(' ').Append(Text.RtlText.Unfix(m.Name));
            }
            if (!string.IsNullOrEmpty(s.Critical))
                sb.Append(", ").Append(Text.RtlText.Unfix(s.Critical!));
            return sb.ToString();
        }
    }
}
