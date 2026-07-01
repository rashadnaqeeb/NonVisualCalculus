using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    public class EntityNamingTests
    {
        private static string Resolve(string? name, string? title = null, bool named = false, string cat = WorldTaxonomy.Container)
            => EntityNaming.Resolve(name, title, named, cat);

        [Fact]
        public void Container_SpeaksTheObjectNoun_NotTheLocation()
        {
            // The clean "<location> <object>" name reduces to the object noun, lowercased.
            Assert.Equal("crate", Resolve("Harbor Crate 22"));
            Assert.Equal("crate", Resolve("Fishmarket Crate"));
            Assert.Equal("bucket", Resolve("Yard Bucket"));
            Assert.Equal("money", Resolve("Church Bench Money"));
            Assert.Equal("metalbox", Resolve("Waterlock Metalbox"));
        }

        [Fact]
        public void Container_StripsDuplicateSuffixesBeforeExtracting()
        {
            Assert.Equal("crate", Resolve("Crate (2)"));
            Assert.Equal("money", Resolve("Harbor Wall Money 1 (2)"));
            Assert.Equal("can", Resolve("Can (Clone)"));
        }

        [Fact]
        public void SlugClutter_NounIsBeforeTheUnderscore()
        {
            // "object_index location" - the noun is the token before the underscore.
            Assert.Equal("box", Resolve("box_3 rooftop"));
            Assert.Equal("crate", Resolve("crate_1 gate"));
            Assert.Equal("crate", Resolve("crate_landsend"));
        }

        [Fact]
        public void AdjectiveNoun_KeepsTheNoun()
        {
            // "empty bottle" is adjective-then-noun: the noun is the last word.
            Assert.Equal("bottle", Resolve("empty bottle"));
        }

        [Fact]
        public void HyphenName_NounIsTheLastWord_TitleIgnored()
        {
            // The title here is a location ("RAILING"); the name still yields the better noun.
            Assert.Equal("jacket", Resolve("Filthy-jacket", title: "BOARDWALK / RAILING"));
        }

        [Fact]
        public void LocationLeadingSlug_PrefersSpoilerSafeTitle()
        {
            // "Ice_eternite" - the noun extractor would guess the location "ice"; the title names it.
            Assert.Equal("Eternite", Resolve("Ice_eternite", title: "ICE / ETERNITE", cat: WorldTaxonomy.Other));
            Assert.Equal("Pile Of Eternite",
                Resolve("Eternite_door", title: "YARD / PILE OF ETERNITE", cat: WorldTaxonomy.Other));
        }

        [Fact]
        public void LocationLeadingSlug_UnsafeTitle_FallsBackToExtractedNoun()
        {
            // The title is rejected (a check word), so the pre-underscore token is spoken instead.
            Assert.Equal("stone", Resolve("stone_perc_1", title: "STONE PERC", cat: WorldTaxonomy.Other));
        }

        [Fact]
        public void EmptyName_NoTitle_FallsBackToCategoryWord()
        {
            Assert.Equal("container", Resolve("", cat: WorldTaxonomy.Container));
            Assert.Equal("object", Resolve(null, cat: WorldTaxonomy.Other));
        }

        [Fact]
        public void NamedCharacter_KeepsFullName()
        {
            Assert.Equal("Kim Kitsuragi", Resolve("Kim Kitsuragi", named: true, cat: WorldTaxonomy.Npc));
            Assert.Equal("Cunoesse", Resolve("Cunoesse", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NamedCharacter_HyphenatedName_IsKept()
        {
            // A real display name hyphenates with its capitals intact, so it is not a machine slug and is
            // spoken in full rather than reduced to the generic "person".
            Assert.Equal("Jean-Vicquemare", Resolve("Jean-Vicquemare", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void NamedCharacterSlug_ReadsPerson_NeverTheTitle()
        {
            Assert.Equal("person",
                Resolve("npc_cunoesse", title: "CUNOESSE", named: true, cat: WorldTaxonomy.Npc));
        }

        [Fact]
        public void Door_KeepsCleanNameElseCategoryWord()
        {
            Assert.Equal("door", Resolve("courtyard-door-crypto-garys-apt", cat: WorldTaxonomy.Door));
            Assert.Equal("Whirling Door", Resolve("Whirling Door", cat: WorldTaxonomy.Door));
        }

        [Fact]
        public void Exit_KeepsCleanName()
        {
            Assert.Equal("tent", Resolve("tent", cat: WorldTaxonomy.Exit));
        }
    }
}
