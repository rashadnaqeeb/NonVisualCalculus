using System.Text.RegularExpressions;

namespace DiscoAccess.Core.Text
{
    /// <summary>
    /// Normalizes raw game text for speech. DE labels are TextMeshPro, so they carry rich-text
    /// markup (&lt;color&gt;, &lt;b&gt;, &lt;sprite&gt;, ...) and hard line breaks. Strip the markup,
    /// turn line breaks into sentence breaks so multi-line text reads with a pause, and collapse the
    /// remaining whitespace. Pure and unit-tested.
    /// </summary>
    public static class TextFilter
    {
        private static readonly Regex RichTags = new Regex("<[^>]+>", RegexOptions.Compiled);
        // A line break (with surrounding whitespace) after text that does not already end a sentence.
        private static readonly Regex BreakAfterText = new Regex("(?<=[^\\s.!?,:;])\\s*[\\r\\n]+\\s*", RegexOptions.Compiled);
        // Any remaining line break (after sentence punctuation, where the pause already exists).
        private static readonly Regex LineBreak = new Regex("\\s*[\\r\\n]+\\s*", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex("\\s+", RegexOptions.Compiled);

        public static string Clean(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            string s = RichTags.Replace(raw, string.Empty);
            s = s.Replace("*", string.Empty); // DE marks emphasis as *word*; drop the markers for speech
            s = s.Replace(' ', ' ');   // non-breaking space
            s = s.Replace('​', ' ');   // zero-width space TMP sometimes injects
            s = FoldPunctuation(s);
            s = s.Trim();
            // A line break in multi-line game text (e.g. an options tooltip listing modes on separate
            // lines) becomes a sentence break so it reads with a pause. When the line already ends with
            // sentence punctuation the break is just a space, so the punctuation is not doubled.
            s = BreakAfterText.Replace(s, ". ");
            s = LineBreak.Replace(s, " ");
            s = Whitespace.Replace(s, " ").Trim();
            return s;
        }

        // Fold the Unicode typographic punctuation common in game text (smart dashes, curly quotes,
        // ellipsis) to plain ASCII so it reads cleanly - an em dash in particular is otherwise announced
        // as "dash" and breaks the flow. (Lines carrying multi-byte characters that Prism would drop are
        // kept speakable generally by the speech pipeline's parity nudge; see SpeechPipeline.)
        private static string FoldPunctuation(string s)
        {
            s = s.Replace('–', '-')   // en dash
                 .Replace('—', '-')   // em dash
                 .Replace('―', '-')   // horizontal bar
                 .Replace('‒', '-')   // figure dash
                 .Replace('−', '-')   // minus sign
                 .Replace('‘', '\'')  // left single quote
                 .Replace('’', '\'')  // right single quote / apostrophe
                 .Replace('“', '"')   // left double quote
                 .Replace('”', '"');  // right double quote
            return s.Replace("…", "...");  // ellipsis
        }
    }
}
