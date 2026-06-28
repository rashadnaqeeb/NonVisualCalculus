using System.Collections.Generic;
using DiscoAccess.Core.UI.Nav;
using Xunit;

namespace DiscoAccess.Tests
{
    public class TypeAheadSearchTests
    {
        // Run the search over a fixed item list, returning the index it landed on (-1 if it announced no
        // result), plus the no-match buffer it reported (null if it matched).
        private static (int landed, string? noMatch) Run(TypeAheadSearch search, string buffer, string[] items)
        {
            int landed = -1;
            string? noMatch = null;
            search.OnNoMatch = b => noMatch = b;
            foreach (char c in buffer) search.AddChar(c);
            // AddChar then a single Search reproduces one keystroke's worth of matching for the whole buffer.
            search.Search(items.Length, i => items[i], i => landed = i);
            return (landed, noMatch);
        }

        [Fact]
        public void Prefix_BeatsSubstring()
        {
            // "be": "Beaver" starts with it (prefix tier); "Cabe" only contains it (substring tier).
            var s = new TypeAheadSearch();
            var (landed, noMatch) = Run(s, "be", new[] { "Cabe", "Beaver" });
            Assert.Null(noMatch);
            Assert.Equal(1, landed); // Beaver
        }

        [Fact]
        public void WithinTier_ShorterNameRanksFirst()
        {
            var s = new TypeAheadSearch();
            var (landed, _) = Run(s, "b", new[] { "Beaver", "Bat" });
            Assert.Equal(1, landed); // Bat (3 chars) before Beaver (6)
        }

        [Fact]
        public void NameMatch_BeatsMetadataMatch()
        {
            // Our focus text is "label, role, value"; a match in the name (pre-comma) outranks one in the
            // metadata tail, even when both are otherwise equal.
            var s = new TypeAheadSearch();
            var (landed, _) = Run(s, "apple", new[] { "Banana, apple", "Apple, fruit" });
            Assert.Equal(1, landed); // "Apple, fruit" - matched in the name, not the trailing "apple"
        }

        [Fact]
        public void NoMatch_ReportsBuffer_AndLandsNowhere()
        {
            var s = new TypeAheadSearch();
            var (landed, noMatch) = Run(s, "z", new[] { "Bat", "Cat" });
            Assert.Equal(-1, landed);
            Assert.Equal("z", noMatch);
            Assert.Equal(0, s.ResultCount);
        }

        [Fact]
        public void RepeatSameLetter_CyclesStartsWithResults()
        {
            var s = new TypeAheadSearch();
            var landings = new List<int>();
            s.AddChar('b');
            s.Search(3, i => new[] { "Bat", "Beaver", "Brewery" }[i], i => landings.Add(i));
            // Second 'b' collapses to a single-letter cycle through the starts-with results.
            s.AddChar('b');
            s.Search(3, i => new[] { "Bat", "Beaver", "Brewery" }[i], i => landings.Add(i));
            Assert.Equal(0, landings[0]); // Bat (shortest)
            Assert.Equal(1, landings[1]); // cycles to Beaver
        }

        [Fact]
        public void NavigateResults_WrapsBothWays()
        {
            var s = new TypeAheadSearch();
            var landings = new List<int>();
            s.AddChar('a');
            // "Apple", "Apricot", "Avocado" all start with a -> three results, shortest first.
            s.Search(3, i => new[] { "Apricot", "Apple", "Avocado" }[i], i => { landings.Add(i); });
            landings.Clear();
            s.NavigateResults(-1); // from first, wrap to last
            Assert.Single(landings);
            s.NavigateResults(1); // back to first
            Assert.Equal(2, landings.Count);
            Assert.NotEqual(landings[0], landings[1]);
        }

        [Fact]
        public void Abbreviation_MatchesWordPrefixTokens()
        {
            // "ga pi" matches "gas pipe" via the space-delimited word-prefix tier.
            var s = new TypeAheadSearch();
            var (landed, noMatch) = Run(s, "ga pi", new[] { "water valve", "gas pipe" });
            Assert.Null(noMatch);
            Assert.Equal(1, landed);
        }

        [Fact]
        public void Diacritics_AreIgnored()
        {
            var s = new TypeAheadSearch();
            var (landed, _) = Run(s, "seance", new[] { "Drama", "Séance" });
            Assert.Equal(1, landed);
        }
    }
}
