using DiscoAccess.Core.Text;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoAccess.Module
{
    /// <summary>
    /// Reads a UI label as the game's natural-case localized source rather than its on-screen form. DE
    /// styles most labels ALL-CAPS by uppercasing the string when it sets <c>.text</c> (the TMP fontStyle
    /// stays Normal, so the caps live in the text itself, not a render style), which a screen reader
    /// voices oddly. The underlying I2 term still resolves to natural case ("Continue", "Thinker"), so
    /// prefer that. Lives in the module because it reads the live Localize term, game state Core cannot
    /// see; the case fix therefore can't sit in Core's pure TextFilter.
    /// </summary>
    public static class GameLocalization
    {
        /// <summary>Resolve an I2 term to the current language, or null when the term is empty.</summary>
        public static string Translate(string term)
        {
            if (string.IsNullOrEmpty(term))
                return null;
            return LocalizationManager.GetTranslation(term, false, 0, true, false, null, null, true);
        }

        /// <summary>
        /// A label's natural-case reading. The DE button bracket frame ("[ LOAD ]") is dropped first so the
        /// caption can be recased and never reads as "left bracket". When the label then carries a Localize
        /// whose translation, uppercased, is exactly the displayed text, the display is just that source
        /// rendered ALL-CAPS, so return the cased source. Otherwise (no term, or the display differs because
        /// it is parameterized or dynamic) keep the de-bracketed display, so this can never corrupt a value
        /// or recase a genuine acronym.
        /// </summary>
        public static string Cased(TMP_Text label) => Cased(label.text, label.GetComponent<Localize>());

        /// <summary>The same natural-case reading for a legacy uGUI <see cref="Text"/> label.</summary>
        public static string Cased(Text label) => Cased(label.text, label.GetComponent<Localize>());

        private static string Cased(string display, Localize localize)
        {
            display = UiLabel.StripBrackets(display);
            if (localize == null)
                return display;
            string source = Translate(localize.Term);
            if (!string.IsNullOrEmpty(source) && source.ToUpperInvariant() == display)
                return source;
            return display;
        }

        /// <summary>
        /// The spoken caption for an icon button that shows no text of its own, read from its image
        /// Localize term, or null when the control is not such a button. DE pairs a button's localized
        /// image (term "..._IMG") with a caption term "Buttons/..._TEXT" (the Load and Save buttons are
        /// image only); resolve and return that caption so the control is not silent.
        /// </summary>
        public static string ImageButtonLabel(GameObject control)
        {
            var localize = control.GetComponent<Localize>();
            string term = localize != null ? localize.Term : null;
            if (string.IsNullOrEmpty(term) || !term.EndsWith("_IMG"))
                return null;

            string name = term.Substring(0, term.Length - "_IMG".Length);
            int slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name.Substring(slash + 1);
            // The image term carries no category; its caption lives in the Buttons category.
            return Translate("Buttons/" + name + "_TEXT");
        }
    }
}
