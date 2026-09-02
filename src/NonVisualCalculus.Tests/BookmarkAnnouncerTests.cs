using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class BookmarkAnnouncerTests
    {
        [Fact]
        public void Compose_ReadsNameThenDistance()
        {
            Assert.Equal("harbor gate, 12 meters", BookmarkAnnouncer.Compose("harbor gate", 12, WalkRoute.Direct));
        }

        [Fact]
        public void Compose_SingularMeter()
        {
            Assert.Equal("my room, 1 meter", BookmarkAnnouncer.Compose("my room", 1, WalkRoute.Direct));
        }

        [Fact]
        public void Compose_ZeroDistance_ReadsUnderAMeter()
        {
            Assert.Equal("here spot, less than a meter", BookmarkAnnouncer.Compose("here spot", 0, WalkRoute.Direct));
        }

        [Fact]
        public void Compose_Unreachable_AppendsCantReach()
        {
            Assert.Equal("balcony, 7 meters, can't reach", BookmarkAnnouncer.Compose("balcony", 7, WalkRoute.None));
        }

        [Fact]
        public void Compose_Detour_AppendsDetour()
        {
            Assert.Equal("apartment courtyard, 14 meters, detour required",
                         BookmarkAnnouncer.Compose("apartment courtyard", 14, WalkRoute.Detour));
        }

        [Fact]
        public void Compose_UnreachableWithReason_SpeaksTheReasonInsteadOfCantReach()
        {
            Assert.Equal("dark storeroom, 9 meters, requires a flashlight",
                         BookmarkAnnouncer.Compose("dark storeroom", 9, WalkRoute.None, reason: "requires a flashlight"));
        }

        [Fact]
        public void Compose_Preset_MarksAfterDistance()
        {
            Assert.Equal("church, 40 meters, preset", BookmarkAnnouncer.Compose("church", 40, WalkRoute.Direct, preset: true));
        }

        [Fact]
        public void Compose_UnreachablePreset_KeepsCantReachLast()
        {
            Assert.Equal("church, 40 meters, preset, can't reach",
                         BookmarkAnnouncer.Compose("church", 40, WalkRoute.None, preset: true));
        }

        [Fact]
        public void Compose_DetourPreset_KeepsDetourLast()
        {
            Assert.Equal("apartment courtyard, 14 meters, preset, detour required",
                         BookmarkAnnouncer.Compose("apartment courtyard", 14, WalkRoute.Detour, preset: true));
        }
    }
}
