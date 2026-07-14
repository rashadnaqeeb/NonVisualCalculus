using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class OrbNamingTests
    {
        [Fact]
        public void TextOverride_Wins()
        {
            Assert.Equal("Most of Mullen",
                OrbNaming.Resolve("Most of Mullen", "teaser", "PLAZA ORB / crack"));
        }

        [Fact]
        public void Morsel_UsedWhenNoOverride()
        {
            Assert.Equal("A faint scratching, just out of reach",
                OrbNaming.Resolve(null, "A faint scratching, just out of reach", "PLAZA ORB / crack"));
        }

        [Fact]
        public void Slug_ClueLeadsWithOrbWord()
        {
            Assert.Equal("crack orb", OrbNaming.Resolve(null, null, "PLAZA ORB / crack"));
        }

        [Fact]
        public void Slug_MultiWordClueSurvives()
        {
            Assert.Equal("halogen watermarks orb",
                OrbNaming.Resolve("", "", "KINEEMA ORB / halogen watermarks"));
        }

        [Fact]
        public void Slug_AreaPrefixDropped()
        {
            Assert.Equal("hum aid macaronis orb",
                OrbNaming.Resolve(null, null, "JAM ORB / hum aid macaronis"));
        }

        [Fact]
        public void TitleWithoutScaffolding_TakesWholeClue()
        {
            Assert.Equal("crack orb", OrbNaming.Resolve(null, null, "crack"));
        }

        [Fact]
        public void NoClue_FallsBackToBareOrbWord()
        {
            Assert.Equal("orb", OrbNaming.Resolve(null, null, "  "));
            Assert.Equal("orb", OrbNaming.Resolve(null, null, "PLAZA ORB / "));
        }

        [Fact]
        public void RidesPlayer_SpeaksTheThoughtOrbWord_NeverTheTitle()
        {
            // A thought-cabinet orb's title is meta text that would also name the thought before the
            // player gains it, so only the type word speaks - in every language.
            Assert.Equal("thought orb",
                OrbNaming.Resolve(null, null, "THOUGHT / SORRY COP", ridesPlayer: true));
        }

        [Fact]
        public void TranslatedGame_DropsTheEnglishClue_ForTheBareOrbWord()
        {
            // The clue is English dev data: where the game speaks another language, an untranslated
            // clue gives way to the (translatable) type word.
            Assert.Equal("orb",
                OrbNaming.Resolve(null, null, "PLAZA ORB / crack", englishClues: false));
        }
    }
}
