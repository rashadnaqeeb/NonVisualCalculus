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

        // Prism rejects some valid multi-byte UTF-8 as InvalidUtf8 by byte position, dropping the whole
        // line; folding typographic punctuation to ASCII keeps every line speakable (Drama's en dash).
        [Theory]
        [InlineData("Play the actor. Lie – and detect lies.", "Play the actor. Lie - and detect lies.")]
        [InlineData("dramatic — pause", "dramatic - pause")]
        [InlineData("it’s a “quote”", "it's a \"quote\"")]
        [InlineData("wait… stop", "wait... stop")]
        public void Clean_FoldsTypographicPunctuationToAscii(string input, string expected)
        {
            Assert.Equal(expected, TextFilter.Clean(input));
        }

        [Fact]
        public void Clean_FoldsDashesQuotesEllipsisToAscii()
        {
            string cleaned = TextFilter.Clean("smart – — ‘ ’ “ ” …");
            Assert.All(cleaned, c => Assert.True(c < 128, $"non-ASCII char U+{(int)c:X4} survived"));
        }

        // Characters with no ASCII reading (e.g. the approx-equals sign) are left for the screen reader to
        // pronounce; the speech pipeline keeps the line speakable, so the filter no longer rewrites them.
        [Fact]
        public void Clean_KeepsNonFoldedSymbols()
        {
            Assert.Equal("Write: ≈50.", TextFilter.Clean("Write: ≈50."));
        }

        // DE marks emphasis with *asterisks*; they are stripped so the words read without the markers.
        [Theory]
        [InlineData("I want to talk about *you*.", "I want to talk about you.")]
        [InlineData("a *field autopsy*", "a field autopsy")]
        public void Clean_StripsEmphasisAsterisks(string input, string expected)
        {
            Assert.Equal(expected, TextFilter.Clean(input));
        }
    }
}
