using System.Collections.Generic;
using System.Numerics;
using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class BookmarkFileTests
    {
        private static List<Bookmark> Parse(string text, out List<string> warnings)
        {
            var list = new List<string>();
            var result = BookmarkFile.Parse(text, list.Add);
            warnings = list;
            return result;
        }

        [Fact]
        public void RoundTrips_ScenesNamesAndPositions()
        {
            var original = new List<Bookmark>
            {
                new Bookmark("Martinaise-ext", "harbor gate", new Vector3(12.5f, 0.125f, -3.25f)),
                new Bookmark("Whirling-int-f2", "my room", new Vector3(-0.75f, 4f, 9.0625f)),
            };

            var parsed = Parse(BookmarkFile.Serialize(original), out var warnings);

            Assert.Empty(warnings);
            Assert.Equal(2, parsed.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].Scene, parsed[i].Scene);
                Assert.Equal(original[i].Name, parsed[i].Name);
                Assert.Equal(original[i].Position, parsed[i].Position);
            }
        }

        [Fact]
        public void Parse_SkipsCommentsAndBlankLines()
        {
            string text = "# a comment\n\r\n  \nMartinaise-ext|spot|1|2|3\n";
            var parsed = Parse(text, out var warnings);
            Assert.Empty(warnings);
            Assert.Single(parsed);
            Assert.Equal("spot", parsed[0].Name);
        }

        [Fact]
        public void Parse_WarnsAndSkipsMalformedLines_KeepingTheRest()
        {
            string text = "Martinaise-ext|first|1|2|3\n"
                        + "not a bookmark line\n"
                        + "Martinaise-ext|missing coordinate|1|2\n"
                        + "Martinaise-ext|bad number|1|2|three\n"
                        + "|no scene|1|2|3\n"
                        + "Martinaise-ext|second|4|5|6\n";
            var parsed = Parse(text, out var warnings);
            Assert.Equal(4, warnings.Count);
            Assert.Equal(2, parsed.Count);
            Assert.Equal("first", parsed[0].Name);
            Assert.Equal("second", parsed[1].Name);
        }

        [Fact]
        public void Parse_ToleratesWindowsLineEndings()
        {
            var parsed = Parse("Martinaise-ext|spot|1|2|3\r\n", out var warnings);
            Assert.Empty(warnings);
            Assert.Single(parsed);
            Assert.Equal("Martinaise-ext", parsed[0].Scene);
        }

        [Fact]
        public void Clean_StripsSeparatorControlChars_AndCollapsesWhitespace()
        {
            Assert.Equal("harbor gate", BookmarkFile.Clean("  harbor \t|\n gate  "));
            Assert.Equal("", BookmarkFile.Clean(null));
            Assert.Equal("", BookmarkFile.Clean(" \t|| "));
        }

        [Fact]
        public void Serialize_CleansNamesSoTheLineStaysParseable()
        {
            var withSeparator = new[] { new Bookmark("scene", "a|b\nc", new Vector3(1, 2, 3)) };
            var parsed = Parse(BookmarkFile.Serialize(withSeparator), out var warnings);
            Assert.Empty(warnings);
            Assert.Single(parsed);
            Assert.Equal("a b c", parsed[0].Name);
        }
    }
}
