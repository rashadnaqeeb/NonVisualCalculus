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
    /// <skill> <name>, (plus|minus) <n> <modifier>". Only the connectives are authored; the skill and
    /// modifier names are game text.
    /// </summary>
    public static class CheckRollAnnouncer
    {
        public static string Compose(CheckRollState s)
        {
            int modSum = 0;
            for (int i = 0; i < s.Modifiers.Count; i++)
                modSum += s.Modifiers[i].Bonus;
            int total = s.Die1 + s.Die2 + s.SkillValue - modSum;

            var sb = new StringBuilder();
            sb.Append(total).Append('/').Append(s.BaseTarget).Append(": ");
            sb.Append(Strings.Strings.CheckRolled).Append(' ')
              .Append(s.Die1).Append(' ').Append(Strings.Strings.CheckPlus).Append(' ').Append(s.Die2);
            sb.Append(", ").Append(Strings.Strings.CheckPlus).Append(' ').Append(s.SkillValue);
            if (!string.IsNullOrEmpty(s.SkillName))
                sb.Append(' ').Append(s.SkillName);
            for (int i = 0; i < s.Modifiers.Count; i++)
            {
                CheckRollModifier m = s.Modifiers[i];
                int effect = -m.Bonus;
                string word = effect >= 0 ? Strings.Strings.CheckPlus : Strings.Strings.CheckMinus;
                int magnitude = effect < 0 ? -effect : effect;
                sb.Append(", ").Append(word).Append(' ').Append(magnitude).Append(' ').Append(m.Name);
            }
            return sb.ToString();
        }
    }
}
