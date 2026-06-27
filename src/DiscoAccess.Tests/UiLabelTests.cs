using DiscoAccess.Core.Text;
using Xunit;

namespace DiscoAccess.Tests
{
    public class UiLabelTests
    {
        [Theory]
        [InlineData("[ DELETE SELECTED]", "DELETE SELECTED")]
        [InlineData("[ LOAD ]", "LOAD")]
        [InlineData("[CONTINUE]", "CONTINUE")]
        public void StripBrackets_RemovesButtonFrame(string input, string expected)
        {
            Assert.Equal(expected, UiLabel.StripBrackets(input));
        }

        [Fact]
        public void StripBrackets_LeavesPlainLabelUnchanged()
        {
            Assert.Equal("Quick Save (Backup)", UiLabel.StripBrackets("Quick Save (Backup)"));
        }

        [Fact]
        public void StripBrackets_Empty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, UiLabel.StripBrackets(""));
        }
    }
}
