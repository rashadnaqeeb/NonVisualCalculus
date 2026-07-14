using NonVisualCalculus.Core.Text;
using Xunit;

namespace NonVisualCalculus.Tests
{
    // The Core composition behind the world-bark reader: joining a bark's speaker and line. The speaker name
    // and line come from the live Subtitle at runtime; here they stand in as fixed strings.
    public class BarkReaderTests
    {
        [Fact]
        public void Compose_NamedSpeaker_LeadsWithColon()
        {
            Assert.Equal("Cuno: Cuno doesn't care.", BarkText.Compose("Cuno", "Cuno doesn't care."));
        }

        [Fact]
        public void Compose_NoSpeaker_LineStandsAlone()
        {
            Assert.Equal("...and now, the weather.", BarkText.Compose("", "...and now, the weather."));
            Assert.Equal("...and now, the weather.", BarkText.Compose(null, "...and now, the weather."));
        }

        [Fact]
        public void Compose_NoLine_Empty()
        {
            Assert.Equal("", BarkText.Compose("Cuno", ""));
            Assert.Equal("", BarkText.Compose("Cuno", null));
            Assert.Equal("", BarkText.Compose(null, null));
        }

        [Fact]
        public void Compose_CleansMarkup()
        {
            Assert.Equal("Kim: Understood.", BarkText.Compose("Kim", "<b>Understood.</b>"));
        }
    }
}
