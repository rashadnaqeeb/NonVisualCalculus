using System.Collections.Generic;
using System.Linq;
using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class PresetBookmarksTests
    {
        private const string Martinaise = "Martinaise-ext";

        [Fact]
        public void All_NamesResolveNonEmptyAndDistinct()
        {
            List<string> names = PresetBookmarks.All.Select(p => p.Name()).ToList();
            Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void All_LiveOnTheOneMartinaiseScene()
        {
            Assert.All(PresetBookmarks.All, p => Assert.Equal(Martinaise, p.Scene));
        }

        [Fact]
        public void For_UnknownScene_ReturnsNothing()
        {
            Assert.Empty(PresetBookmarks.For("Lobby", day: 5));
        }

        [Fact]
        public void For_BeforeDayThree_HidesTheCoast()
        {
            List<PresetBookmark> day1 = PresetBookmarks.For(Martinaise, day: 1);
            Assert.Equal(PresetBookmarks.All.Count - 2, day1.Count);
            Assert.All(day1, p => Assert.Equal(1, p.MinDay));
        }

        [Fact]
        public void For_FromDayThree_ListsEverything()
        {
            Assert.Equal(PresetBookmarks.All.Count, PresetBookmarks.For(Martinaise, day: 3).Count);
        }
    }
}
