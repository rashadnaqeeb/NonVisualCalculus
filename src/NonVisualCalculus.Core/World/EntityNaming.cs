using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static NonVisualCalculus.Core.Strings.Strings;

namespace NonVisualCalculus.Core.World
{
    /// <summary>
    /// Resolves the spoken name for a world thing from the raw game data a Module proxy extracts (the
    /// engine-free half of the naming rule, so it is unit-tested). The best source is the game's own authored
    /// name: the examine conversation's CONVERSANT actor, localized ("Cuno", "Pile of Eternite", "Drainage
    /// Pipe") - the same name a sighted player reads on examining the thing. Where that is absent (a container
    /// has no conversation) the name is reconstructed from the designer's <c>GameObject.name</c>, speaking the
    /// object NOUN rather than a bare category word - "crate" and "money" are useful where "container" is not.
    ///
    /// - A named character prefers the authored name (which drops a location prefix, "Yard Cuno" to "Cuno");
    ///   else a clean <c>GameObject.name</c>; else "person", never a raw conversation title (which would leak).
    /// - An exit prefers its authored name too, which the proxy resolves as the localized DESTINATION it leads
    ///   to ("Whirling-in-Rags", "Cuno's shack"), so the player hears where a door goes; else its clean name,
    ///   else the category word "exit". A plain door prefers its authored examine header ("Door, Room #1"),
    ///   then the mod's authored name for the known dev-named doors (the fallback table, shared with locked
    ///   exit-doors that lead nowhere), then its own clean name, else "door".
    /// - A container prefers the authored name; failing that it is named from the <c>GameObject.name</c> by
    ///   its object TYPE when a generic container word is present ("box", "crate", "money", "trash can"),
    ///   position-independent so the designer's word order and location decoration stop mattering ("Box
    ///   Backroom" and "FV money Shack" reduce to "box"/"money"). A container that is a SPECIFIC item (no
    ///   generic type word) keeps its full flavor name instead, cleaned: a trailing generic "container" tag
    ///   is dropped ("Phasmid Nest Container" to "phasmid nest") and a leading run of location tokens (the
    ///   current area name, compass words) is stripped ("martinaise east photo of rene" to "photo of rene",
    ///   "South Shack" to "shack"). See <see cref="ResolveContainer"/>.
    /// - A prop (everything else) prefers the authored name; failing that it speaks the object noun pulled
    ///   from the <c>GameObject.name</c> (the last word of "Harbor Crate 22" is "crate"; the slug clutter
    ///   "box_3 rooftop" carries its noun before the underscore, "box"), and as a last resort a
    ///   spoiler-filtered conversation title for the location-slug form ("Ice_eternite").
    ///
    /// <paramref name="authoredName"/> is whatever authored display name the proxy resolved for this thing
    /// from the game (the conversant actor for a character or prop, the destination area for an exit); this
    /// engine-free half decides how it combines with the <c>GameObject.name</c> fallbacks.
    /// </summary>
    public static class EntityNaming
    {
        public static string Resolve(string? rawName, string? authoredName, string? conversationTitle,
                                     bool isNamedCharacter, string category,
                                     IReadOnlyCollection<string>? areaTokens = null,
                                     string? contentName = null)
        {
            string name = Normalize(rawName);
            string? authored = CleanAuthored(authoredName);

            // A named character: the game's authored actor name reads cleanest (it drops the "Yard Cuno"
            // location prefix to "Cuno"); else a clean GameObject.name; else the generic word, never a title.
            if (isNamedCharacter)
                return authored ?? (name.Length > 0 && !IsSlug(name) ? name : WorldThingPerson);

            // A plain door: the game's authored examine header when one resolves ("Door, Room #1", "Blue
            // door", "Padlocked Door"), else the mod's authored name for the known dev-named doors (the
            // fallback table), else its own clean name, else the category word.
            if (category == WorldTaxonomy.Door)
                return authored
                       ?? AuthoredDoorFallback(name)
                       ?? (name.Length > 0 && !IsSlug(name) ? name : TypeWord(category));

            // An exit: the destination it leads to when the proxy resolved one, plus the portal type read from
            // the GameObject.name ("courtyard-door-..." to "door", "...stairs..." to "stairs"), so the player
            // hears "Whirling in Rags door" or "floor 2 stairs". With no resolved destination but a door named
            // for a specific outdoor spot ("Balcony", "Roof" - see SpotFromDoorName, set up by the proxy for
            // exterior doors), that spot leads, defaulting to "door" ("balcony door"). Failing both, a clean
            // bespoke name, else the plain type word.
            if (category == WorldTaxonomy.Exit)
            {
                string? typeKw = ExitTypeKeyword(name);
                if (authored != null) return ExitNamed(authored, typeKw ?? WorldThingExit);
                string? fallback = AuthoredDoorFallback(name);
                if (fallback != null) return fallback;
                string? spot = SpotFromDoorName(name);
                if (spot != null) return ExitNamed(spot, typeKw ?? WorldThingDoor);
                return name.Length > 0 && !IsSlug(name) ? name : (typeKw ?? WorldThingExit);
            }

            // Containers and props share the authored-name preference ("Pile of Eternite", "Policeman Cloak").
            if (authored != null) return authored;

            // A container names itself by object TYPE or, for a specific item, its full flavor name; only
            // containers get this, so a prop that reads like one (the interactable trash can, a pile) keeps
            // the noun extractor below untouched.
            if (category == WorldTaxonomy.Container)
                return ResolveContainer(name, areaTokens, contentName);

            // Props: the object noun from the name. For the slug clutter whose leading token is a location
            // ("Ice_eternite"), a spoiler-safe title reads better than the noun extractor's guess, so try it
            // before extracting; the title-less clutter ("box_3 rooftop") just extracts its noun.
            if (name.Length > 0 && FirstToken(name).IndexOf('_') >= 0)
            {
                string? slugTitle = SpoilerSafeTitle(conversationTitle);
                if (slugTitle != null) return slugTitle;
            }
            if (name.Length > 0) return ExtractNoun(name);

            string? title = SpoilerSafeTitle(conversationTitle);
            return title ?? TypeWord(category);
        }

        // Generic container words: a word that is ONLY a container type, carrying no identity of its own, so
        // a container named with one is "just a box/crate/money/..." and its location decoration is noise -
        // speak the type. Kept deliberately to purely-generic words (never "suit", "jacket", "nest", which
        // are the specific item and must survive), so the set only ever loses a nicer name, never speaks a
        // worse one. Grows as new areas surface new container words. Matched as whole lowercase tokens.
        private static readonly HashSet<string> ContainerTypeWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "box", "crate", "can", "bottle", "barrel", "dumpster", "bucket", "jar", "sack",
            "bag", "pot", "cup", "chest", "canister", "metalbox", "trashcan", "drawer", "locker",
            "cabinet", "safe", "wallet", "grate", "vent", "money",
        };

        // Martinaise sub-district slugs (the English internal names, as they appear leading a container's
        // GameObject.name: "Yard Woodpile", "Landsend Rock", "Ice Pillars", "Jam Cigars"). Stripped as a
        // leading location run off a specific item's flavor name, so the noun survives without its district
        // prefix ("cigars", not "jam cigars" - "jam" is the Traffic Jam district). Map-specific and matched
        // as whole lowercase tokens, the district mirror of the noun list; extended per map.
        private static readonly HashSet<string> DistrictSlugs = new HashSet<string>(StringComparer.Ordinal)
        {
            "yard", "pier", "harbour", "harbor", "fishmarket", "landsend", "coast", "ice", "village",
            "fv", "plaza", "canal", "waterlock", "boardwalk", "jam", "traffic",
        };

        // The generic bucket tag a designer appends to a specific container ("Phasmid Nest Container"): too
        // generic to speak, so it is dropped when flavor remains, leaving the item's real name.
        private static bool IsBucketWord(string t) => t == "container" || t == "containers";

        // The eight compass words, stripped as a leading location run off a specific container's flavor name
        // ("South Shack" to "shack"). Matched as whole lowercase tokens.
        private static readonly HashSet<string> CompassWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest",
        };

        // The spoken name for a container. Tier one: a generic container word anywhere in the name is the
        // type, spoken position-independent so word order and location decoration stop mattering ("Box
        // Backroom" and "FV money Shack" reduce to "box"/"money", two-word "trash can" before "can"). Tier
        // two: a specific item (no generic type word) keeps its full flavor name, cleaned - a trailing
        // generic "container" tag dropped and a leading run of location tokens (the current area name, a
        // compass word) stripped, so "martinaise east photo of rene" reads "photo of rene" while "Leopard
        // Suit" stays "leopard suit". Tier one's spoken type word comes from the strings table, so it
        // translates; a tier-two flavor name is dev-side English with no game string to reuse, so it does
        // not. Only the token alias ("buoya" to "buoy A") is authored.
        private static string ResolveContainer(string name, IReadOnlyCollection<string>? areaTokens,
                                               string? contentName)
        {
            string[] tokens = Tokenize(name);
            if (tokens.Length == 0) return WorldThingContainer;

            string? typeWord = FindContainerType(tokens);
            if (typeWord != null) return typeWord;

            // A flavor-named container IS its visible item (the trousers on the chair, the shoe on the
            // floor), so when the caller resolved that single guaranteed item's display name - which it
            // does only in a non-English game, where the dev name below is untranslatable English (see
            // the module's ContentItemName for the policy) - that localized name speaks instead. Never
            // for a type-word container (above): a generic box's contents are hidden until opened, and
            // speaking them would tell a blind player what a sighted one cannot see.
            if (!string.IsNullOrEmpty(contentName)) return contentName!;

            var flavor = new List<string>(tokens);
            if (flavor.Count > 1 && IsBucketWord(flavor[flavor.Count - 1]))
                flavor.RemoveAt(flavor.Count - 1);
            while (flavor.Count > 1 && IsLeadingLocation(flavor[0], areaTokens))
                flavor.RemoveAt(0);

            var sb = new StringBuilder();
            for (int i = 0; i < flavor.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(AliasToken(flavor[i]));
            }
            // The flavor name is English dev data with no game string in any language, so it speaks
            // as-is; a non-English game substitutes the content item's localized name above instead.
            return sb.ToString();
        }

        // The generic container type named anywhere in the tokens, two-word types ("trash can") before the
        // single word they contain ("can"), else the first single-word type in reading order (so "FV money
        // Shack" takes "money", not a later word). Null when the container is a specific item. The match is
        // on the language-invariant English token; the SPOKEN word comes from the strings table
        // (Strings.ContainerWord), singular or plural to follow the dev name ("Jam Crates" reads "crates").
        private static string? FindContainerType(string[] tokens)
        {
            for (int i = 0; i + 1 < tokens.Length; i++)
                if (tokens[i] == "trash" && tokens[i + 1] == "can") return ContainerWord("trash can", plural: false);
            foreach (string t in tokens)
            {
                string? singular = ContainerTypeSingular(t);
                if (singular != null) return ContainerWord(singular, plural: singular != t);
            }
            return null;
        }

        // The canonical singular for a generic container token, allowing a regular English plural so
        // "crates"/"boxes" count as "crate"/"box". Null when the token is no container word.
        private static string? ContainerTypeSingular(string t)
        {
            if (ContainerTypeWords.Contains(t)) return t;
            if (t.Length > 4 && t.EndsWith("es", StringComparison.Ordinal))
            {
                string stem = t.Substring(0, t.Length - 2);           // boxes -> box
                if (ContainerTypeWords.Contains(stem)) return stem;
            }
            if (t.Length > 3 && t.EndsWith("s", StringComparison.Ordinal))
            {
                string stem = t.Substring(0, t.Length - 1);           // crates -> crate
                if (ContainerTypeWords.Contains(stem)) return stem;
            }
            return null;
        }

        // A leading token that is location scaffolding on a specific container's name: a compass word, a known
        // sub-district slug ("Yard Woodpile" to "woodpile"), or the current scene's area token(s) the module
        // passes in (the English scene-name stems, e.g. "martinaise" for Martinaise-ext), which prefix a slug
        // like "martinaise-east-photo-of-rene".
        private static bool IsLeadingLocation(string token, IReadOnlyCollection<string>? areaTokens)
        {
            if (CompassWords.Contains(token)) return true;
            if (DistrictSlugs.Contains(token)) return true;
            if (areaTokens != null)
                foreach (string a in areaTokens)
                    if (string.Equals(a, token, StringComparison.Ordinal)) return true;
            return false;
        }

        // Speak a container flavor token, applying the internal-spelling aliases (only "buoya" to "buoy A"
        // today), so the numbered buoy reads as its letter rather than "buoya".
        private static string AliasToken(string token)
            => token == "buoya" ? WorldBuoyA : token;

        // Split a container name into lowercase word tokens on any separator (space, underscore, hyphen),
        // dropping single-character noise (the "R" in "Vent R") and bare index numbers, so a slug and a
        // spaced name reduce to the same token list.
        private static string[] Tokenize(string name)
        {
            var list = new List<string>();
            foreach (string t in Regex.Split(name, @"[\s_\-]+"))
            {
                string w = t.ToLowerInvariant();
                if (w.Length < 2) continue;
                if (Regex.IsMatch(w, @"^\d+$")) continue;
                list.Add(w);
            }
            return list.ToArray();
        }

        // The mod's own authored names for specific doors, keyed by the normalized GameObject.name. Only for
        // doors the game gives NEITHER an examine actor NOR a readable object name: the Whirling floor-2
        // bathroom doors and the Capeside locked doors are raw dev names that leak cast names ("Whirling
        // Door Bathroom Klaasje") or reduce to a bare "door" among many. Values come from the Strings table
        // so they localize with the rest of the authored set; a door with any game string keeps it (the
        // authored examine header is checked first).
        private static readonly Dictionary<string, string> AuthoredDoorFallbacks =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Kitsuragi's is the door joining the shared bathroom to his room - spoken as the connection
            // it is, so the bathroom's two doors are distinguishable by ear.
            ["Whirling Door Bathroom Kitsuragi"] = WorldThingConnectingDoor,
            ["Whirling Door Bathroom Klaasje"] = WorldThingBathroomDoor,
            ["Door Apartment no dialogue(bathroom)"] = WorldThingBathroomDoor,
            ["Door Apartment no dialogue(empty room)"] = WorldThingLockedDoor,
            ["Door Apartment 10 Locked"] = WorldThingLockedDoor,
            ["Locked-door_capeside-1"] = WorldThingLockedDoor,
            ["Locked-door_capeside-2"] = WorldThingLockedDoor,
            ["Locked-door_capeside-3"] = WorldThingLockedDoor,
        };

        private static string? AuthoredDoorFallback(string normalizedName)
            => AuthoredDoorFallbacks.TryGetValue(normalizedName, out string? spoken) ? spoken : null;

        // The game's authored display name (a conversant actor, or an exit's destination area), rejected only
        // when unusable: empty, a machine id (an underscore), the player ("You"/"Player"), or a
        // mechanical/conditional token. There is no length cap: every long actor name in the dialogue
        // database is a genuine display title (a paperback's full title, "FALN Sneakers on a Pedestal of
        // Speakers"), and rejecting one falls back to a noun pulled from a machine slug, which can read as
        // garbage ("clickable_humanitarian_sneakers" would speak as "clickable"). Hyphens are display
        // punctuation in a curated name ("Whirling-in-Rags"), spoken as a space, so they are converted, not
        // treated as the slug marker they are in a raw GameObject.name. A "Name, the Title" appositive keeps
        // just the name before the comma ("Garte, the Cafeteria Manager" to "Garte") - recognized by the
        // "the" that opens the title - while a comma QUALIFIER is kept whole ("Door, Room #1", "Key, Room
        // #1"): there the part after the comma is the distinguishing half, and cutting it leaves a generic
        // word.
        private static string? CleanAuthored(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (raw!.IndexOf('_') >= 0) return null;              // a machine id (actor_5), not a display name
            string s = Regex.Replace(raw.Replace('-', ' ').Trim(), @"\s+", " ");
            int comma = s.IndexOf(',');
            if (comma >= 0 && s.Substring(comma + 1).TrimStart().StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, comma).Trim();                 // "Garte, the Cafeteria Manager" -> "Garte"
            if (s.Length == 0) return null;
            if (string.Equals(s, "You", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Player", StringComparison.OrdinalIgnoreCase)) return null;
            string[] words = Regex.Split(s, @"\s+");
            foreach (string w in words)
                foreach (string meta in MetaTokens)
                    if (string.Equals(w, meta, StringComparison.OrdinalIgnoreCase))
                        return null;
            return s;
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

        // What an exit's destination is called, given the destination's localized area name, the current
        // area's localized name, and the destination's scene id. Prefer the destination name when it is
        // distinct from where you are - a different building ("Martinaise", "Whirling-in-Rags") or a floor the
        // game gives its own name (Doomed Commercial's ground floor is "Bookstore"). When the destination
        // shares the current name (all Whirling floors are "Whirling-in-Rags"), that name says nothing new, so
        // fall back to the floor/level word from the scene id suffix ("Whirling-int-f2" to "floor 2",
        // "Doomed-commerce-int-s1" to "basement"). Null when neither yields anything, so the caller uses the
        // exit's own clean name or the plain type word. A scene id whose game label misnames the place gets
        // the mod's authored name first (see AuthoredAreaName).
        public static string? ExitDestinationLabel(string? destAreaId, string? destLocalizedName, string? currentLocalizedName)
        {
            string? authored = AuthoredAreaName(destAreaId);
            if (authored != null) return authored;
            if (!string.IsNullOrEmpty(destLocalizedName)
                && !string.Equals(destLocalizedName, currentLocalizedName, StringComparison.OrdinalIgnoreCase))
                return destLocalizedName;
            return LevelLabel(destAreaId);
        }

        // The mod's authored name for a destination whose game area label misnames the place: the
        // secretary's office, the union boss's office, and the container yard's cargo container are
        // all localized "Harbour" (the only three areas that are), so the game's labels cannot tell
        // those doors from a harbour gate or from each other. Resolved per call so a runtime
        // language switch is honoured.
        private static string? AuthoredAreaName(string? destAreaId)
        {
            if (string.Equals(destAreaId, "Secretary-int", StringComparison.OrdinalIgnoreCase))
                return WorldPlaceSecretaryOffice;
            if (string.Equals(destAreaId, "Union-boss-int", StringComparison.OrdinalIgnoreCase))
                return WorldPlaceUnionOffice;
            if (string.Equals(destAreaId, "Union-container-int", StringComparison.OrdinalIgnoreCase))
                return WorldPlaceCargoContainer;
            return null;
        }

        /// <summary>The floor/level word from a scene id's suffix: "-f&lt;n&gt;" is a numbered floor
        /// ("floor 2"), "-s&lt;n&gt;" a basement/sublevel ("basement"). Null when the id carries no level
        /// suffix (the exterior, a flat interior). Names exit destinations above and the location readout.</summary>
        public static string? LevelLabel(string? areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return null;
            Match f = Regex.Match(areaId!, @"-f(\d+)", RegexOptions.IgnoreCase);
            if (f.Success) return FloorNumber(f.Groups[1].Value);
            if (Regex.IsMatch(areaId!, @"-s\d+", RegexOptions.IgnoreCase)) return WorldBasement;
            return null;
        }

        // The portal type read from an exit's GameObject.name slug, as an authored (localizable) word, or null
        // when the name carries no known portal word (a tent flap is just "tent"; a spot door is "Balcony").
        // "stair" covers "stairs"/"stairwell"; the match is on the language-invariant English slug.
        private static string? ExitTypeKeyword(string name)
        {
            string lo = name.ToLowerInvariant();
            if (lo.Contains("stair")) return WorldThingStairs;
            if (lo.Contains("elevator") || lo.Contains("lift")) return WorldThingElevator;
            if (lo.Contains("door")) return WorldThingDoor;
            if (lo.Contains("gate")) return WorldThingGate;
            if (lo.Contains("ladder")) return WorldThingLadder;
            if (lo.Contains("boat")) return WorldThingBoat;
            return null;
        }

        // A specific outdoor spot named by an exit's own GameObject name ("Balcony", "Roof"), lowercased for
        // speech. Null when the name is a slug id or is itself a portal-type word (a door/stairs/etc., a type
        // not a place). The proxy uses this only for doors leading to the main exterior, where the coarse area
        // name ("Martinaise") would otherwise hide that the door opens onto a particular balcony or roof.
        public static string? SpotFromDoorName(string? rawName)
        {
            string n = Normalize(rawName);
            if (n.Length == 0 || IsSlug(n)) return null;
            if (ExitTypeKeyword(n) != null) return null;
            return n.ToLowerInvariant();
        }

        // Whether an entity's GameObject.name IS this actor: the designer names the object after the actor's
        // internal name modulo casing and separators ("fuckTheWorld" carries the actor "Fuck the World").
        // Compared on letters and digits alone, and only whole - a partial overlap ("Yard Cuno" vs "Cuno")
        // or an empty name is no identity.
        public static bool NameMatchesActor(string? entityName, string? actorName)
        {
            string a = LettersAndDigits(entityName);
            return a.Length > 0 && string.Equals(a, LettersAndDigits(actorName), StringComparison.Ordinal);
        }

        private static string LettersAndDigits(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s!.Length);
            foreach (char c in s!)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

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
