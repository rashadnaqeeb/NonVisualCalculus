using System.Text.RegularExpressions;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Resolves the spoken name for a sense orb from the raw fields a Module proxy extracts, so the naming is
    /// engine-free and unit-tested. An orb has no game-authored short label - a sighted player just sees a
    /// glowing dot and only learns what it is on triggering it - so the name is built from what the orb data
    /// does carry, in priority: an explicit text override, then a morsel teaser (which the proxy localizes),
    /// else the orb conversation's slug title reduced to its clue. The clue leads and the type word "orb"
    /// follows ("crack orb"), distinguishing word first. The clue is English dev data, so it speaks only
    /// where it can be understood: as-is in an English game, else the bare localized type word - a
    /// translatable word beats an untranslatable clue. A
    /// thought-cabinet orb riding the character never speaks its title at all: that is meta text ("THOUGHT /
    /// SORRY COP") naming a thought the player has not gained, so it reads as the thought-orb type word.
    /// </summary>
    public static class OrbNaming
    {
        public static string Resolve(string? textOverride, string? morselText, string? conversationTitle,
                                     bool ridesPlayer = false, bool englishClues = true)
        {
            string? over = Clean(textOverride);
            if (over != null) return over;
            string? morsel = Clean(morselText);
            if (morsel != null) return morsel;
            if (ridesPlayer) return WorldThingThoughtOrb;
            string? clue = Clue(conversationTitle);
            return clue != null && englishClues ? OrbNamed(clue) : WorldThingOrb;
        }

        // The clue in an orb conversation's slug title: strip the leading "<area> ORB / " (or a bare "ORB ")
        // scaffolding and keep what follows, whitespace collapsed. "PLAZA ORB / crack" reduces to "crack",
        // "KINEEMA ORB / halogen watermarks" to "halogen watermarks". Null when nothing meaningful remains.
        private static string? Clue(string? title)
        {
            string? t = Clean(title);
            if (t == null) return null;
            t = Regex.Replace(t, @"^.*\bORB\b\s*/\s*", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"^\s*ORB\b\s*", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t.Length > 0 ? t : null;
        }

        private static string? Clean(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : Regex.Replace(s!.Trim(), @"\s+", " ");
    }
}
