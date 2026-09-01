using System;
using System.Text;

namespace NonVisualCalculus.Core.Text
{
    /// <summary>
    /// Restores logical order for Arabic text the game pre-shaped for display. DE shapes Arabic two
    /// ways. Its RTL fix (I2's ArabicFixer, applied to dialogue, actor names, legacy-Text labels, and
    /// any term fetched with fixForRTL) converts logical Arabic into presentation-form glyphs in
    /// VISUAL order - reversed, with Latin/digit runs kept upright and brackets mirrored - so a
    /// left-to-right renderer draws it correctly. TMP labels with <c>isRightToLeftText</c> instead
    /// carry presentation forms in LOGICAL order (TMP itself reverses at render time). A speech
    /// synthesizer needs the logical original of either, so <see cref="Unfix"/> tells them apart by
    /// the direction of the positional forms (in logical storage a joined group's INITIAL form comes
    /// before its FINAL; reversed storage puts finals first), reverses only visual-order text
    /// (un-mirroring the brackets and restoring the Latin/digit runs), and folds the presentation
    /// forms of both to plain letters (NFKC, which also splits the lam-alef ligatures).
    /// Presentation forms (U+FB50-FDFF, U+FE70-FEFF) never occur in logical unshaped text - a
    /// keyboard or a translation file produces base letters - so their presence marks a shaped string
    /// and the unfix is inert on everything else. Tashkeel marks may land before their base letter
    /// after the reversal; game names carry none, and a synthesizer skips stray marks.
    /// </summary>
    public static class RtlText
    {
        /// <summary>The unfix for text whose storage order is KNOWN logical: a TMP label whose
        /// <c>isRightToLeftText</c> is set (read it through the module's flag-aware helper). TMP
        /// reverses the whole text at render time, so the source stores logical Arabic but
        /// compensates everywhere for that reversal: paired punctuation pre-mirrored, Latin/digit
        /// runs pre-reversed ("Fairweather T-500" stored "فيرويذر 005-T"). Undo both across the
        /// whole string - no direction vote, which a short unjoined word can defeat - then fold the
        /// shaping. Inert on unshaped text.</summary>
        public static string UnfixLogical(string s)
        {
            if (string.IsNullOrEmpty(s) || !HasPresentationForms(s))
                return s;
            s = DropTags(s);
            char[] chars = s.ToCharArray();
            FoldLogicalSpan(chars, 0, chars.Length - 1);
            return FoldShaping(chars);
        }

        // Rich-text tags in a shaped string, in either storage: a tag the game's reversal dragged
        // along arrives with its angle brackets mirrored and contents reversed ("<color=#AB>"
        // arriving as ">BA#=roloc<"), while a tag the game inserted AFTER reversing (the skill-name
        // coloring on the thought-completion bonuses) sits logical ("<color=#AB>") - one string can
        // carry both. No later strip can handle either once this class has run (the logical strip
        // cannot match a mirrored pair, and the logical fold would mirror an intact tag's brackets
        // and reverse its name into exactly that garbage), so every tag is dropped here: in a shaped
        // string an angle-bracket pair is always markup, exactly as the logical tag strip assumes.
        // Each bracket opens a tag in its own direction ('<' logical, '>' mirrored) running to the
        // matching closer; a bracket with no closer is literal text and stays.
        private static string DropTags(string s)
        {
            if (s.IndexOf('<') < 0 && s.IndexOf('>') < 0)
                return s;
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '<' || c == '>')
                {
                    int j = s.IndexOfAny(AngleBrackets, i + 1);
                    if (j >= 0 && s[j] == (c == '<' ? '>' : '<')) { i = j + 1; continue; }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static readonly char[] AngleBrackets = { '<', '>' };

        public static string Unfix(string s)
        {
            if (string.IsNullOrEmpty(s) || !HasPresentationForms(s))
                return s;
            s = DropTags(s);

            // The mod composes spoken lines from mixed parts - a fixed game name, our own logical
            // separator and words ("<name>; east, 3 meters"), a fixed speaker and a fixed line
            // ("<who>: <line>") - and only the game-fixed parts need inverting, each within its own
            // position. So unfix per Arabic CLUSTER: a maximal span from one Arabic-script character
            // to the last one reachable without crossing an ASCII ':' or ';' (our composition
            // separators; Arabic text uses its own ؛ ،) or a line break (the game fixes per LINE, so
            // a break always bounds a fixed unit - reversing across it would swap the lines), interior
            // Latin/digit runs included. A cluster is inverted only when it itself carries
            // presentation forms, so logical Arabic from the mod's own translation table sits
            // untouched next to a fixed game name.
            char[] chars = s.ToCharArray();
            int i = 0;
            while (i < chars.Length)
            {
                if (!IsArabicScript(chars[i])) { i++; continue; }
                int start = i, lastArabic = i;
                bool hasForms = IsPresentationForm(chars[i]);
                for (int j = i + 1; j < chars.Length && chars[j] != ':' && chars[j] != ';'
                                    && chars[j] != '\n' && chars[j] != '\r'; j++)
                {
                    if (!IsArabicScript(chars[j])) continue;
                    lastArabic = j;
                    hasForms |= IsPresentationForm(chars[j]);
                }
                if (hasForms)
                {
                    if (ClusterIsLogicalOrder(chars, start, lastArabic))
                    {
                        // An RTL-flagged TMP's text: already logical, so no reversal (it would
                        // garble) - but it carries the render-reversal compensations, so undo those
                        // within the cluster; the fold below does the rest.
                        FoldLogicalSpan(chars, start, lastArabic);
                    }
                    else
                    {
                        // The fixer keeps Latin/digit runs upright, so a digit run at the cluster's
                        // visual FRONT belongs to the fixed string - it reads at the string's logical
                        // end ("Room 1" puts the 1 at the visual front) - and is pulled into the
                        // reversal. Only the front: digits trailing the Arabic are almost always a
                        // separately composed segment (the game's own "<day> 08:06", our "الطابق 2")
                        // that must stay where it is.
                        UnfixCluster(chars, ExtendStart(chars, start), lastArabic);
                    }
                }
                i = lastArabic + 1;
            }

            return FoldShaping(chars);
        }

        // Presentation-form glyphs to plain letters ("ﻛ" to "ك", lam-alef ligatures to their pair)
        // via NFKC - except the Farsi yeh forms (U+FBFC-FBFF), which the game's shaper uses for ALEF
        // MAKSURA (its ArabicTable maps U+0649 there: the glyphs are dotless, exactly the maksura
        // shape, and no other letter maps into that block) while NFKC would fold them to Farsi yeh
        // (U+06CC), a letter a synthesizer speaks as plain yeh. Restore the maksura first.
        private static string FoldShaping(char[] chars)
        {
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= 'ﯼ' && chars[i] <= 'ﯿ')
                    chars[i] = 'ى';
            return new string(chars).Normalize(NormalizationForm.FormKC);
        }

        // The cluster's front edge pulled backward over an adjacent DIGIT run (spaces included only
        // when a run lies beyond them); anything else stops the extension. Digits only: a leading
        // number is part of the fixed string ("Room 1"), while an edge Latin word is usually the
        // mod's own composition around the name ("moving to <name>") and must stay outside the
        // reversal.
        private static int ExtendStart(char[] chars, int start)
        {
            int k = start - 1, mark = start;
            while (k >= 0)
            {
                char c = chars[k];
                if (c == ' ') { k--; continue; }
                if (!IsDigit(c)) break;
                mark = k;
                k--;
            }
            return mark;
        }

        private static bool IsDigit(char c)
            => (c >= '0' && c <= '9')
               || (c >= 0x0660 && c <= 0x0669)  // Eastern Arabic digits
               || (c >= 0x06F0 && c <= 0x06F9); // Extended (Persian-style) digits

        // Invert the fixer over chars[start..end] (inclusive): reverse back to logical order,
        // un-mirror the paired punctuation the fixer mirrored for display, and flip back the Latin
        // and digit runs the fixer kept upright inside the visual text ("27A" to "A72").
        private static void UnfixCluster(char[] chars, int start, int end)
        {
            Array.Reverse(chars, start, end - start + 1);
            for (int i = start; i <= end; i++)
                chars[i] = Mirror(chars[i]);
            RestoreUprightRuns(chars, start, end);
        }

        // Undo the compensations logical-order text carries for TMP's render reversal, over a span
        // that is entirely such text: paired punctuation is stored pre-mirrored and Latin/digit
        // runs pre-reversed, both anywhere in the span, so swap and re-reverse them in place.
        private static void FoldLogicalSpan(char[] chars, int start, int end)
        {
            for (int i = start; i <= end; i++)
                chars[i] = Mirror(chars[i]);
            RestoreUprightRuns(chars, start, end);
        }

        // Reverse each Latin/digit run back upright, joiners included when they connect two run
        // characters ("005-T" back to "T-500", "00:12" back to "21:00") - a trailing joiner is
        // sentence punctuation and stays put, and a joiner followed by a space (our "<speaker>: "
        // composition) never bridges.
        private static void RestoreUprightRuns(char[] chars, int start, int end)
        {
            int i = start;
            while (i <= end)
            {
                if (!KeepsLogicalOrder(chars[i])) { i++; continue; }
                int runEnd = i;
                for (int j = i + 1; j <= end; j++)
                {
                    if (KeepsLogicalOrder(chars[j])) { runEnd = j; continue; }
                    if (IsRunJoiner(chars[j]) && j < end && KeepsLogicalOrder(chars[j + 1])) continue;
                    break;
                }
                Array.Reverse(chars, i, runEnd - i + 1);
                i = runEnd + 1;
            }
        }

        private static bool IsRunJoiner(char c) => c == '-' || c == '.' || c == ',' || c == '/' || c == ':';

        // Whether a shaped cluster stores its characters in logical order (an RTL-flagged TMP label,
        // which TMP reverses at render time) rather than visual order (the ArabicFixer output). Each
        // Arabic word with both an initial and a final presentation form votes by which comes first
        // in memory: a joined group reads initial-medial-final in logical storage, so finals-first
        // means the string was reversed for display. A tie (short or unjoined words carry no vote)
        // falls to visual order, the majority case, and for a form-free word the two orders are
        // indistinguishable anyway.
        private static bool ClusterIsLogicalOrder(char[] chars, int start, int end)
        {
            int logical = 0, visual = 0;
            int i = start;
            while (i <= end)
            {
                if (!IsArabicScript(chars[i])) { i++; continue; }
                int firstInitial = -1, firstFinal = -1;
                int j = i;
                for (; j <= end && IsArabicScript(chars[j]); j++)
                {
                    int pos = FormPosition(chars[j]);
                    if (pos == PosInitial && firstInitial < 0) firstInitial = j;
                    else if (pos == PosFinal && firstFinal < 0) firstFinal = j;
                }
                if (firstInitial >= 0 && firstFinal >= 0)
                {
                    if (firstInitial < firstFinal) logical++;
                    else visual++;
                }
                i = j;
            }
            return logical > visual;
        }

        private const int PosNone = 0, PosIsolated = 1, PosFinal = 2, PosInitial = 3, PosMedial = 4;

        // Starts of the Presentation Forms-B per-letter blocks: two-form letters run isolated, final;
        // four-form letters isolated, final, initial, medial.
        private static readonly char[] TwoFormBlocks =
        {
            'ﺁ', 'ﺃ', 'ﺅ', 'ﺇ', // alef madda / hamza-above / waw-hamza / hamza-below
            'ﺍ', 'ﺓ', 'ﺩ', 'ﺫ', // alef, teh marbuta, dal, thal
            'ﺭ', 'ﺯ', 'ﻭ', 'ﻯ', // reh, zain, waw, alef maksura
        };
        private static readonly char[] FourFormBlocks =
        {
            'ﺉ', 'ﺏ', 'ﺕ', 'ﺙ', 'ﺝ', // yeh-hamza, beh, teh, theh, jeem
            'ﺡ', 'ﺥ', 'ﺱ', 'ﺵ', 'ﺹ', // hah, khah, seen, sheen, sad
            'ﺽ', 'ﻁ', 'ﻅ', 'ﻉ', 'ﻍ', // dad, tah, zah, ain, ghain
            'ﻑ', 'ﻕ', 'ﻙ', 'ﻝ', 'ﻡ', // feh, qaf, kaf, lam, meem
            'ﻥ', 'ﻩ', 'ﻱ',                     // noon, heh, yeh
        };

        // The positional class of an Arabic Presentation Forms-B glyph, or none for anything else
        // (base letters, tashkeel, and the Forms-A ligature block carry no direction signal).
        private static int FormPosition(char c)
        {
            if (c >= 'ﻵ' && c <= 'ﻼ')     // lam-alef ligatures: iso/fin pairs
                return (c - 'ﻵ') % 2 == 0 ? PosIsolated : PosFinal;
            if (c >= 'ﯼ' && c <= 'ﯿ')     // Farsi yeh forms: the shaper's alef maksura (see FoldShaping)
                return PosIsolated + (c - 'ﯼ');
            if (c < 'ﺀ' || c > 'ﻴ')
                return PosNone;
            if (c == 'ﺀ')                      // hamza: isolated only
                return PosIsolated;
            foreach (char b in TwoFormBlocks)
            {
                if (c == b) return PosIsolated;
                if (c == b + 1) return PosFinal;
            }
            foreach (char b in FourFormBlocks)
                if (c >= b && c <= b + 3)
                    return PosIsolated + (c - b);
            return PosNone;
        }

        // Any Arabic-script character, base or presentation form: what anchors a cluster.
        private static bool IsArabicScript(char c)
            => (c >= 0x0600 && c <= 0x06FF)     // Arabic
               || (c >= 0x0750 && c <= 0x077F)  // Arabic Supplement
               || IsPresentationForm(c);

        private static bool IsPresentationForm(char c)
            => (c >= 0xFB50 && c <= 0xFDFF)     // Arabic Presentation Forms-A
               || (c >= 0xFE70 && c <= 0xFEFE); // Arabic Presentation Forms-B

        private static bool HasPresentationForms(string s)
        {
            foreach (char c in s)
                if (IsPresentationForm(c))
                    return true;
            return false;
        }

        // A character the fixer leaves in logical order within the visual string: Latin letters
        // (ASCII and Latin-1/Extended) and digits, Western or Eastern Arabic.
        private static bool KeepsLogicalOrder(char c)
        {
            if (c >= '0' && c <= '9') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 0x00C0 && c <= 0x024F && char.IsLetter(c)) return true; // accented Latin
            if (c >= 0x0660 && c <= 0x0669) return true; // Eastern Arabic digits
            if (c >= 0x06F0 && c <= 0x06F9) return true; // Extended (Persian-style) digits
            return false;
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '<': return '>';
                case '>': return '<';
                default: return c;
            }
        }
    }
}
