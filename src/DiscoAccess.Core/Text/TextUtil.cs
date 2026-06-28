using System.Globalization;
using System.Text;

namespace DiscoAccess.Core.Text
{
    /// <summary>
    /// Text helpers for matching (distinct from <see cref="TextFilter"/>, which cleans game text for
    /// speech). Pure and unit-testable.
    /// </summary>
    public static class TextUtil
    {
        /// <summary>Fold accents away for matching ("Séance" matches "seance"); ligatures œ/æ expand.
        /// Ported from OniAccess (VisionNotIncluded) via wotr-access, with permission.</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            for (int i = 0; i < decomposed.Length; i++)
            {
                char c = decomposed[i];
                switch (c)
                {
                    case 'œ': case 'Œ': sb.Append("oe"); break;
                    case 'æ': case 'Æ': sb.Append("ae"); break;
                    default:
                        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
