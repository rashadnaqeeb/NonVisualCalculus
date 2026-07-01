using System;
using System.Globalization;
using System.Text.RegularExpressions;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.World
{
    /// <summary>
    /// Resolves the spoken name for a world thing from the raw game data a Module proxy extracts (the
    /// engine-free half of the naming rule, so it is unit-tested). There is no clean display-name field, so
    /// the name is reconstructed from the designer's <c>GameObject.name</c> and, as a cautious fallback, the
    /// examine-conversation title. The guiding idea: speak the object NOUN, not a bare category word - "crate"
    /// and "money" are useful where "container" is not - and let the spoken position tell duplicates apart.
    ///
    /// - A named character keeps their full name ("Kim Kitsuragi"); a character with only a slug id reads
    ///   "person", never their conversation title (which would leak).
    /// - A door or exit keeps a clean name, else its category word ("door" / "exit") - already specific
    ///   enough.
    /// - Everything else (containers, props) speaks the object noun pulled from the name: the last word of a
    ///   clean "Harbor Crate 22" is "crate"; the slug clutter "box_3 rooftop" carries its noun before the
    ///   underscore, "box". Where that slug's leading word is a location instead (the "Ice_eternite" form), a
    ///   spoiler-filtered title names it better ("Eternite").
    /// </summary>
    public static class EntityNaming
    {
        public static string Resolve(string? rawName, string? conversationTitle, bool isNamedCharacter, string category)
        {
            string name = Normalize(rawName);

            // A named character: keep their full name; a bare slug falls to "person", never the title.
            if (isNamedCharacter)
                return name.Length > 0 && !IsSlug(name) ? name : WorldThingPerson;

            // Doors and exits: a clean name if there is one, else the category word (specific enough).
            if (category == WorldTaxonomy.Door || category == WorldTaxonomy.Exit)
                return name.Length > 0 && !IsSlug(name) ? name : TypeWord(category);

            // Containers and props: speak the object noun. For the slug clutter whose leading token is a
            // location ("Ice_eternite"), a spoiler-safe title reads better than the noun extractor's guess,
            // so try it first; the title-less clutter ("box_3 rooftop") just extracts its noun.
            if (name.Length > 0 && FirstToken(name).IndexOf('_') >= 0)
            {
                string? slugTitle = SpoilerSafeTitle(conversationTitle);
                if (slugTitle != null) return slugTitle;
            }
            if (name.Length > 0) return ExtractNoun(name);

            string? title = SpoilerSafeTitle(conversationTitle);
            return title ?? TypeWord(category);
        }

        // Light cleanup: drop Unity's "(Clone)" and a trailing duplicate suffix (" (2)", " 2", "_3"), then
        // collapse whitespace. Separators are kept (their presence marks a slug, handled below).
        private static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw!.Replace("(Clone)", "").Trim();
            s = Regex.Replace(s, @"\s*\(\d+\)$", "").Trim(); // " (2)" duplicate suffix
            s = Regex.Replace(s, @"[ _]\d+$", "").Trim();    // " 2" / "_3" duplicate suffix
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        // A name is a slug (machine-generated, not for display) when it carries a separator the clean names
        // never use: an underscore, or a hyphen joining lowercase word characters ("crypto-garys-apt"). The
        // hyphen test requires lowercase/digit on both sides so a proper hyphenated display name, which keeps
        // its capitals ("Jean-Vicquemare"), is not mistaken for a slug and read as the generic word.
        private static bool IsSlug(string name)
            => name.IndexOf('_') >= 0 || Regex.IsMatch(name, @"[a-z0-9]-[a-z0-9]");

        private static string FirstToken(string name)
        {
            int sp = name.IndexOf(' ');
            return sp < 0 ? name : name.Substring(0, sp);
        }

        // The object noun spoken for a container/prop, lowercased like the common noun it is. The slug
        // clutter leads with an "object_index" token ("box_3 rooftop", "crate_landsend") - the noun is the
        // part before the underscore. Otherwise the noun is the last alphabetic word, which fits both the
        // clean "<Location> <Object>" name ("Harbor Crate 22" to "crate") and the "<adjective> <noun>" form
        // ("empty bottle" to "bottle").
        private static string ExtractNoun(string name)
        {
            string first = FirstToken(name);
            int us = first.IndexOf('_');
            if (us > 0) return first.Substring(0, us).ToLowerInvariant();

            string? last = null;
            foreach (string w in Regex.Split(name, @"[\s\-]+"))
                if (Regex.IsMatch(w, @"^[A-Za-z]{2,}$")) last = w;
            return (last ?? name).ToLowerInvariant();
        }

        // Meta/mechanical tokens that mark a conversation title as unsafe to speak (they describe a hidden
        // check or branch a sighted player cannot see). Matched case-insensitively as whole words.
        private static readonly string[] MetaTokens =
            { "PERC", "CHECK", "VISCAL", "COMP", "IF", "EARLIER", "LATER", "CLICKED", "THREAD" };

        // The spoiler filter for the examine-conversation title: strip the ZAUM "<area> / " (and "ORB ")
        // scaffolding, then reject the remainder outright if it looks mechanical or conditional - a meta
        // token, a difficulty number, multiple clauses, or itself a slug. What survives is a short, plain
        // object title, recased from display caps. Conservative by design: the noun extractor and the
        // generic word are always safe, so over-rejecting is the correct failure.
        private static string? SpoilerSafeTitle(string? conversationTitle)
        {
            if (string.IsNullOrWhiteSpace(conversationTitle)) return null;
            string title = conversationTitle!;

            // The leading tag is the area (and any sub-area) name plus " / " - "ICE / ETERNITE",
            // "YARD / PILE OF ETERNITE" - so everything up to the last slash is location scaffolding; keep
            // only the thing after it. The standalone "ORB " prefix has no slash, so strip it first.
            title = Regex.Replace(title.Trim(), @"^\s*ORB\b\s*", "", RegexOptions.IgnoreCase);
            title = Regex.Replace(title, @"^.*/\s*", "").Trim();
            if (title.Length == 0) return null;

            if (IsSlug(title)) return null;                    // an internal id, not a title
            if (Regex.IsMatch(title, @"\d")) return null;      // a difficulty number leaks
            if (title.IndexOf(',') >= 0) return null;          // multiple clauses
            string[] words = Regex.Split(title, @"\s+");
            if (words.Length > 3) return null;                 // a conditional description, not a name
            foreach (string w in words)
                foreach (string meta in MetaTokens)
                    if (string.Equals(w, meta, StringComparison.OrdinalIgnoreCase))
                        return null;

            return TitleCase(title);
        }

        // The titles are display-styled ALL CAPS ("STONE", "FOOTPRINTS"); recase to natural words so the
        // reader does not spell them out.
        private static string TitleCase(string s)
            => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        private static string TypeWord(string category)
        {
            switch (category)
            {
                case WorldTaxonomy.Door: return WorldThingDoor;
                case WorldTaxonomy.Exit: return WorldThingExit;
                case WorldTaxonomy.Container: return WorldThingContainer;
                case WorldTaxonomy.Npc: return WorldThingPerson;
                default: return WorldThingObject;
            }
        }
    }
}
