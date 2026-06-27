using System.Text;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused character-creation archetype from its
    /// <see cref="ArchetypeState"/>. Order follows the house style: the archetype name first (the
    /// distinguishing word), then the mechanical detail a player chooses on (each attribute as
    /// "name value", then the signature skill), then the flavor description last, so a quick navigator
    /// hears the name and stats before the longer text is cut by the next focus. Every word here is the
    /// game's own localized text spoken verbatim; nothing is mod-authored.
    /// </summary>
    public static class ArchetypeAnnouncer
    {
        public static string Compose(ArchetypeState s)
        {
            var sb = new StringBuilder();
            Append(sb, s.Name);
            foreach (ArchetypeAttribute a in s.Attributes)
                Append(sb, a.Name + " " + a.Value);
            Append(sb, s.SignatureSkill);
            Append(sb, s.Description);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string? part)
        {
            if (string.IsNullOrEmpty(part))
                return;
            if (sb.Length > 0)
                sb.Append(", ");
            sb.Append(part);
        }
    }
}
