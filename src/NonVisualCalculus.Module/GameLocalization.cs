using NonVisualCalculus.Core.Text;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonVisualCalculus.Module
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
        /// <summary>Whether the game is running in English, the language of its dev-side data (object
        /// names, orb title clues) - the gate for speaking that data raw versus falling back to
        /// localized sources. An unresolved language reads as English (the conservative default).</summary>
        public static bool IsEnglish
        {
            get
            {
                string code = LocalizationManager.CurrentLanguageCode;
                return string.IsNullOrEmpty(code) || code == "en";
            }
        }

        /// <summary>Resolve an I2 term to the current language, or null when the term is empty.</summary>
        public static string Translate(string term)
        {
            if (string.IsNullOrEmpty(term))
                return null;
            return LocalizationManager.GetTranslation(term, false, 0, true, false, null, null, true);
        }

        /// <summary>
        /// Resolve one of DE's own UI terms through its localization wrapper (the same call the game's
        /// tooltips make, so no category path is needed), falling back to the given authored word when the
        /// term resolves to nothing or echoes its own name (a missing term). fixForRTL is off: the fix
        /// shapes Arabic into visual order for the renderer, and speech needs the logical original.
        /// </summary>
        public static string Term(string term, string fallback)
        {
            string s = LocalizationCustomSystem.LocalizationManager.GetLocalizedTerm(term, false, false);
            return string.IsNullOrEmpty(s) || s == term ? fallback : s;
        }

        /// <summary>
        /// A TMP label's text in speakable logical order. DE shapes Arabic two ways, and the label's own
        /// RTL flag says which: an <c>isRightToLeftText</c> label stores logical order with the
        /// render-reversal compensations (TMP reverses at draw time), while an unflagged label may carry
        /// the fixer's visual-order text, which the heuristic unfix inverts. Prefer this over reading
        /// <c>.text</c> raw anywhere the text can be Arabic - the flag is certainty the heuristic lacks.
        /// </summary>
        public static string Spoken(TMP_Text label)
            => label.isRightToLeftText ? RtlText.UnfixLogical(label.text) : RtlText.Unfix(label.text);

        /// <summary>
        /// A label's natural-case reading. The DE button bracket frame ("[ LOAD ]") is dropped first so the
        /// caption can be recased and never reads as "left bracket". When the label then carries a Localize
        /// whose translation, uppercased, is exactly the displayed text, the display is just that source
        /// rendered ALL-CAPS, so return the cased source. Otherwise (no term, or the display differs because
        /// it is parameterized or dynamic) keep the de-bracketed display, so this can never corrupt a value
        /// or recase a genuine acronym.
        /// </summary>
        public static string Cased(TMP_Text label) => Cased(Spoken(label), label.GetComponent<Localize>());

        /// <summary>The same natural-case reading for a legacy uGUI <see cref="Text"/> label (legacy text
        /// has no RTL flag; DE gives it the fixer's visual-order form, which the heuristic unfix inverts).</summary>
        public static string Cased(Text label) => Cased(RtlText.Unfix(label.text), label.GetComponent<Localize>());

        private static string Cased(string display, Localize localize)
        {
            // The display arrives in logical order (the callers unfix it), so the source comparison
            // below can match for a caseless RTL language, where the term's value IS the unfixed display.
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
            // The image term carries no category; its caption lives in the Buttons category. A caption
            // here is one short phrase; a line break in it is layout to fit the button art ("TAKE\nALL"),
            // so it is collapsed to a space rather than left for the filter's multi-line pause.
            string caption = Translate("Buttons/" + name + "_TEXT");
            return caption == null ? null : string.Join(" ", caption.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
