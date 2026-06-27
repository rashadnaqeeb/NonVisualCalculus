using System.Text.RegularExpressions;

namespace DiscoAccess.Core.Text
{
    /// <summary>
    /// Normalizes a UI control's own label for speech, separate from the general <see cref="TextFilter"/>
    /// that cleans all game text. DE frames button captions in square brackets ("[ LOAD ]"), which a
    /// screen reader voices as "left bracket ... right bracket". Drop those brackets here, scoped to UI
    /// labels so brackets that carry meaning elsewhere (item descriptions, dialogue) are left untouched.
    /// Pure and unit-tested.
    /// </summary>
    public static class UiLabel
    {
        private static readonly Regex Brackets = new Regex("[\\[\\]]", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex("\\s+", RegexOptions.Compiled);

        public static string StripBrackets(string? label)
        {
            if (string.IsNullOrEmpty(label))
                return label ?? string.Empty;
            string s = Brackets.Replace(label, " ");
            return Whitespace.Replace(s, " ").Trim();
        }
    }
}
