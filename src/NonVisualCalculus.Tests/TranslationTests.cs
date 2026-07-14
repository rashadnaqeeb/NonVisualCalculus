using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    /// <summary>
    /// The translation mechanism: the file parser, the plural rules, the override/fallback lookup, and
    /// the word-order templates the world naming composes through. Every test that loads a table resets
    /// it in finally, so the rest of the suite keeps its English expectations.
    /// </summary>
    public class TranslationTests
    {
        private static void Loaded(Action assertions, params (string Key, string Value)[] entries)
        {
            var dict = new Dictionary<string, string>();
            foreach (var (key, value) in entries) dict[key] = value;
            try
            {
                Translation.Load(dict);
                assertions();
            }
            finally
            {
                Translation.Reset();
            }
        }

        // ---- file parsing ----

        [Fact]
        public void ParseFile_ReadsKeyValues_SkipsCommentsAndBlanks()
        {
            var entries = Translation.ParseFile(
                "# comment\n\nWorldHere = ici\r\nWorldMoving = déplacement\n", out List<string> errors);

            Assert.Empty(errors);
            Assert.Equal("ici", entries["WorldHere"]);
            Assert.Equal("déplacement", entries["WorldMoving"]);
        }

        [Fact]
        public void ParseFile_SplitsAtFirstEquals_SoValuesMayContainOne()
        {
            var entries = Translation.ParseFile("Step = étape {0} = {1}\n", out _);
            Assert.Equal("étape {0} = {1}", entries["Step"]);
        }

        [Fact]
        public void ParseFile_ReportsMalformedLines_WithLineNumbers()
        {
            Translation.ParseFile("WorldHere = ici\nno separator here\n", out List<string> errors);
            Assert.Single(errors);
            Assert.Contains("line 2", errors[0]);
        }

        // ---- load, override, fallback ----

        [Fact]
        public void LoadedValue_OverridesEnglish_AndMissingKeysFallBack()
        {
            Loaded(() =>
            {
                Assert.Equal("porte", Strings.WorldThingDoor);
                Assert.Equal("gate", Strings.WorldThingGate); // not in the file: English
            }, ("WorldThingDoor", "porte"));
        }

        [Fact]
        public void Reset_RestoresEnglish()
        {
            Loaded(() => Assert.Equal("porte", Strings.WorldThingDoor), ("WorldThingDoor", "porte"));
            Assert.Equal("door", Strings.WorldThingDoor);
        }

        [Fact]
        public void Load_SetsAsideUnknownAndEmptyKeys_AndUnknownPluralRule()
        {
            var report = Translation.Load(new Dictionary<string, string>
            {
                ["WorldThingDoor"] = "porte",
                ["NoSuchKey"] = "x",
                ["WorldThingGate"] = "  ",
                [Translation.PluralKey] = "klingon",
            });
            try
            {
                Assert.Equal(1, report.Applied);
                Assert.Equal("NoSuchKey", Assert.Single(report.UnknownKeys));
                Assert.Equal("WorldThingGate", Assert.Single(report.EmptyKeys));
                Assert.Equal("klingon", report.UnknownPluralRule);
                Assert.Equal("gate", Strings.WorldThingGate); // the empty value never applied
            }
            finally
            {
                Translation.Reset();
            }
        }

        [Fact]
        public void EveryStringsProperty_ResolvesAgainstTheTable()
        {
            // A typo'd key inside an accessor would otherwise only crash the first time that screen
            // speaks; touching every accessor here makes it a test failure instead.
            foreach (PropertyInfo prop in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (prop.PropertyType == typeof(string))
                    Assert.False(string.IsNullOrEmpty((string?)prop.GetValue(null)), prop.Name + " is empty");
        }

        // ---- plural rules ----

        [Fact]
        public void SlavicRule_PicksOneFewMany()
        {
            Loaded(() =>
            {
                Assert.Equal("1 заряд", Strings.HealCharges(1));
                Assert.Equal("3 заряда", Strings.HealCharges(3));
                Assert.Equal("5 зарядов", Strings.HealCharges(5));
                Assert.Equal("11 зарядов", Strings.HealCharges(11));
                Assert.Equal("21 заряд", Strings.HealCharges(21));
            }, (Translation.PluralKey, "slavic"), ("HealCharges", "{0} заряд|{0} заряда|{0} зарядов"));
        }

        [Fact]
        public void ArabicRule_PicksSixForms()
        {
            Loaded(() =>
            {
                Assert.Equal("zero", Strings.HealCharges(0));
                Assert.Equal("one", Strings.HealCharges(1));
                Assert.Equal("two", Strings.HealCharges(2));
                Assert.Equal("5 few", Strings.HealCharges(5));
                Assert.Equal("15 many", Strings.HealCharges(15));
                Assert.Equal("100 other", Strings.HealCharges(100));
            }, (Translation.PluralKey, "arabic"),
               ("HealCharges", "zero|one|two|{0} few|{0} many|{0} other"));
        }

        [Fact]
        public void FrenchRule_KeepsZeroSingular()
        {
            Loaded(() => Assert.Equal("0 mètre", Strings.WorldDistance(0)),
                (Translation.PluralKey, "french"),
                ("WorldDistanceZero", "0 mètre"),
                ("WorldDistanceMeters", "{0} mètre|{0} mètres"));
            // (distance 0 takes the dedicated zero string; the rule matters for counts like 1 vs 2)
            Loaded(() =>
            {
                Assert.Equal("1 mètre", Strings.WorldDistance(1));
                Assert.Equal("2 mètres", Strings.WorldDistance(2));
            }, (Translation.PluralKey, "french"), ("WorldDistanceMeters", "{0} mètre|{0} mètres"));
        }

        [Fact]
        public void TooFewPluralForms_ClampToTheLast()
        {
            // An arabic-rule file that only bothered with two forms: counts past the list speak the
            // last form (the "other" convention) rather than crashing or going silent.
            Loaded(() => Assert.Equal("7 uses", Strings.ItemUses(7)),
                (Translation.PluralKey, "arabic"), ("ItemUses", "{0} use|{0} uses"));
        }

        [Fact]
        public void UntranslatedPluralKey_KeepsEnglishForms_UnderAForeignRule()
        {
            // The file sets a slavic rule but does not translate ItemUses: the English default's two
            // forms are picked with the ENGLISH rule, so 3 is not mis-indexed into a missing form.
            Loaded(() => Assert.Equal("3 uses", Strings.ItemUses(3)),
                (Translation.PluralKey, "slavic"));
        }

        // ---- word-order templates ----

        [Fact]
        public void Templates_PlaceTheSlotWhereTheTranslationSays()
        {
            Loaded(() =>
            {
                Assert.Equal("aller vers Kim", Strings.WorldMovingTo("Kim"));
                Assert.Equal("1,50 réal", Strings.WorldMoney(150));
            }, ("WorldMovingTo", "aller vers {0}"), ("WorldMoney", "{0},{1} réal"));
        }

        [Fact]
        public void ExitTemplate_CanFlipDestinationAndTypeWord()
        {
            Loaded(() =>
            {
                string name = EntityNaming.Resolve(
                    "courtyard-door-2", "Whirling", null, false, WorldTaxonomy.Exit);
                Assert.Equal("porte Whirling", name);
            }, ("WorldExitNamed", "{1} {0}"), ("WorldThingDoor", "porte"));
        }

        [Fact]
        public void OrbTemplate_CanLeadWithTheTypeWord()
        {
            Loaded(() => Assert.Equal("orbe crack", OrbNaming.Resolve(null, null, "PLAZA ORB / crack")),
                ("WorldOrbNamed", "orbe {0}"));
        }

        [Fact]
        public void ContainerTypeWords_SpeakTranslated_MatchingStaysEnglish()
        {
            Loaded(() =>
            {
                // The dev names stay English in every game language; only the spoken word changes.
                Assert.Equal("caisse", EntityNaming.Resolve(
                    "Harbor Crate 22", null, null, false, WorldTaxonomy.Container));
                Assert.Equal("caisses", EntityNaming.Resolve(
                    "Jam Crates Right", null, null, false, WorldTaxonomy.Container));
            }, ("ContainerWord_crate", "caisse|caisses"));
        }

        [Fact]
        public void CompassList_Translates_AndAShortListFallsBackToEnglishPerEntry()
        {
            Loaded(() =>
            {
                Assert.Equal("nord", Strings.WorldCompass(0));
                Assert.Equal("nord-est", Strings.WorldCompass(1));
                // The file only translated two bearings: the rest speak English, never a clamped
                // NEIGHBOUR bearing, which would be wrong information.
                Assert.Equal("east", Strings.WorldCompass(2));
                Assert.Equal("south", Strings.WorldCompass(4));
            }, ("WorldCompass", "nord|nord-est"));
        }

        [Fact]
        public void FloorTemplate_ComposesTheLevelWord()
        {
            Loaded(() =>
            {
                string? name = EntityNaming.ExitDestinationLabel("Whirling-int-f2", "Whirling", "Whirling");
                Assert.Equal("étage 2", name);
            }, ("WorldFloorNumber", "étage {0}"));
        }

        // ---- a flavor-named container speaks its single item's localized name ----

        [Fact]
        public void FlavorContainer_SpeaksItsResolvedContentName()
        {
            // The proxy resolved the container's single guaranteed item ("Boot-cut pants" holds the
            // Flare-cut Trousers): the item's localized display name speaks instead of the dev name.
            Assert.Equal("Flare-cut Trousers", EntityNaming.Resolve(
                "Boot-cut pants", null, null, false, WorldTaxonomy.Container,
                contentName: "Flare-cut Trousers"));
        }

        [Fact]
        public void GenericContainer_NeverSpeaksItsContents()
        {
            // A type-word container's contents are hidden until opened; the content name is ignored
            // even when the caller resolved one, so a blind player never hears what a sighted one
            // cannot see.
            Assert.Equal("box", EntityNaming.Resolve(
                "Backroom Box", null, null, false, WorldTaxonomy.Container,
                contentName: "Flare-cut Trousers"));
        }

        [Fact]
        public void FlavorContainer_WithoutAContentName_KeepsItsFlavorName()
        {
            Assert.Equal("boot cut pants", EntityNaming.Resolve(
                "Boot-cut pants", null, null, false, WorldTaxonomy.Container));
        }

        // ---- the committed template ----

        [Fact]
        public void CommittedEnglishTemplate_MatchesDumpTemplate()
        {
            // lang/en.txt is the translator-facing copy of the table. When this fails after adding a
            // string, regenerate it: write Strings.DumpTemplate() to lang/en.txt (UTF-8, LF).
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NonVisualCalculus.slnx")))
                dir = dir.Parent;
            Assert.NotNull(dir);

            string committed = File.ReadAllText(Path.Combine(dir!.FullName, "lang", "en.txt"))
                .Replace("\r\n", "\n");
            Assert.Equal(Strings.DumpTemplate(), committed);
        }

    }
}
