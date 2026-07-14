using System.Collections.Generic;
using System.Text;

namespace NonVisualCalculus.Core.Text
{
    /// <summary>
    /// Joins the non-empty parts of one spoken line, un-RTL-fixing each part first (<see
    /// cref="RtlText.Unfix"/>). A part that is game text can arrive display-shaped (presentation forms
    /// in visual order), and it must invert within its own part: once parts are joined, no character
    /// reliably marks where one string ends and the next begins - the game's fixer repositions
    /// punctuation inside a line, and a short logical word of non-joining letters is byte-identical to
    /// a fixed fragment. The unfix is inert on logical and non-Arabic text, so authored parts pass
    /// through unchanged. Every composition that joins game text with other text belongs here (or must
    /// unfix its game strings itself); the whole-line unfix in <see cref="TextFilter.Clean"/> only
    /// handles a line that is one single fixed string.
    /// </summary>
    public static class SpokenLine
    {
        /// <summary>Non-empty parts joined with ", " (the standard announcement separator).</summary>
        public static string Join(params string?[] parts) => Join(", ", parts);

        public static string Join(string separator, IEnumerable<string?> parts)
        {
            var sb = new StringBuilder();
            foreach (string? part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                if (sb.Length > 0)
                    sb.Append(separator);
                sb.Append(RtlText.Unfix(part!));
            }
            return sb.ToString();
        }
    }
}
