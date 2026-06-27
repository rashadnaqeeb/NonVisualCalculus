using System.Text;

namespace DiscoAccess.Core.UI
{
    /// <summary>
    /// Composes the spoken line for a focused save/load list entry from its <see cref="SaveEntryState"/>.
    /// Order follows the house style: the save name first (the distinguishing word a navigator scans for),
    /// then its date and time. Every part is the game's own text spoken verbatim; nothing is mod-authored.
    /// </summary>
    public static class SaveEntryAnnouncer
    {
        public static string Compose(SaveEntryState s)
        {
            var sb = new StringBuilder();
            Append(sb, s.Name);
            Append(sb, s.Date);
            Append(sb, s.Time);
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
