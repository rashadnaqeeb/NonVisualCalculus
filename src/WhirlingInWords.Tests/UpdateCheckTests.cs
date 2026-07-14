using WhirlingInWords.Core.Updates;
using Xunit;

namespace WhirlingInWords.Tests
{
    /// <summary>
    /// The update check's pure halves: reading the release version out of the GitHub JSON, and
    /// deciding whether it is an upgrade over the running version. The fetch and the announcement
    /// timing live against the engine and are exercised in-game.
    /// </summary>
    public class UpdateCheckTests
    {
        [Theory]
        [InlineData("{\"tag_name\": \"v1.2.0\", \"assets\": []}", "1.2.0")]
        [InlineData("{\"tag_name\": \"1.2.0\"}", "1.2.0")]
        [InlineData("{\"tag_name\":\"v2.0.0-beta\"}", "2.0.0-beta")]
        [InlineData("{ \"tag_name\" : \"v1.0.1\" }", "1.0.1")]
        public void ParseLatestVersion_ReadsTheTag(string json, string expected)
        {
            Assert.Equal(expected, UpdateCheck.ParseLatestVersion(json));
        }

        [Theory]
        [InlineData("{\"message\": \"Not Found\"}")]
        [InlineData("")]
        [InlineData("not json at all")]
        public void ParseLatestVersion_NoTag_ReturnsNull(string json)
        {
            Assert.Null(UpdateCheck.ParseLatestVersion(json));
        }

        [Theory]
        [InlineData("1.0.0", "1.0.1")]
        [InlineData("1.0.0", "1.1.0")]
        [InlineData("1.9.0", "1.10.0")]
        public void IsNewer_NewerRelease_True(string current, string latest)
        {
            Assert.True(UpdateCheck.IsNewer(current, latest));
        }

        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.1.0", "1.0.9")]
        [InlineData("1.10.0", "1.9.0")]
        public void IsNewer_SameOrOlderRelease_False(string current, string latest)
        {
            Assert.False(UpdateCheck.IsNewer(current, latest));
        }

        [Fact]
        public void IsNewer_UnparsableVersions_FallBackToInequality()
        {
            // A renamed scheme still gets offered; an identical string never does.
            Assert.True(UpdateCheck.IsNewer("1.0.0", "release-2"));
            Assert.False(UpdateCheck.IsNewer("release-2", "release-2"));
        }
    }
}
