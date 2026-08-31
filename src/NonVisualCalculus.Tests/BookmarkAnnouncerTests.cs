using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class BookmarkAnnouncerTests
    {
        [Fact]
        public void Compose_ReadsNameThenDistance()
        {
            Assert.Equal("harbor gate, 12 meters", BookmarkAnnouncer.Compose("harbor gate", 12, reachable: true));
        }

        [Fact]
        public void Compose_SingularMeter()
        {
            Assert.Equal("my room, 1 meter", BookmarkAnnouncer.Compose("my room", 1, reachable: true));
        }

        [Fact]
        public void Compose_ZeroDistance_ReadsUnderAMeter()
        {
            Assert.Equal("here spot, less than a meter", BookmarkAnnouncer.Compose("here spot", 0, reachable: true));
        }

        [Fact]
        public void Compose_Unreachable_AppendsCantReach()
        {
            Assert.Equal("balcony, 7 meters, can't reach", BookmarkAnnouncer.Compose("balcony", 7, reachable: false));
        }

        [Fact]
        public void Compose_Preset_MarksAfterDistance()
        {
            Assert.Equal("church, 40 meters, preset", BookmarkAnnouncer.Compose("church", 40, reachable: true, preset: true));
        }

        [Fact]
        public void Compose_UnreachablePreset_KeepsCantReachLast()
        {
            Assert.Equal("church, 40 meters, preset, can't reach",
                         BookmarkAnnouncer.Compose("church", 40, reachable: false, preset: true));
        }
    }
}
