using DiscoAccess.Core.Text;
using Xunit;

namespace DiscoAccess.Tests
{
    public class TextFilterTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("plain text", "plain text")]
        public void Clean_HandlesEmptyAndPlain(string? input, string expected)
        {
            Assert.Equal(expected, TextFilter.Clean(input));
        }

        [Fact]
        public void Clean_StripsRichTextTags()
        {
            Assert.Equal("Detective", TextFilter.Clean("<color=#ff0000><b>Detective</b></color>"));
        }

        [Fact]
        public void Clean_StripsSpriteAndKeepsSurroundingText()
        {
            Assert.Equal("Talk to Kim", TextFilter.Clean("Talk to <sprite=3> Kim"));
        }

        [Fact]
        public void Clean_CollapsesSpacesAndTabs()
        {
            Assert.Equal("line one line two", TextFilter.Clean("line   one\t line two"));
        }

        [Fact]
        public void Clean_NewlineBecomesSentenceBreak()
        {
            Assert.Equal("line one. line two", TextFilter.Clean("line one\n\n   line two\t"));
        }

        [Fact]
        public void Clean_NewlineAfterSentencePunctuation_NotDoubled()
        {
            Assert.Equal("Easier for dyslexics. Unavailable in Chinese.",
                TextFilter.Clean("Easier for dyslexics.\nUnavailable in Chinese."));
        }

        [Fact]
        public void Clean_MultiLineList_ReadsAsSentences()
        {
            Assert.Equal("Full, all voiced. Psychological, except narration. Classic, intros only",
                TextFilter.Clean("Full, all voiced\nPsychological, except narration\nClassic, intros only"));
        }
    }
}
