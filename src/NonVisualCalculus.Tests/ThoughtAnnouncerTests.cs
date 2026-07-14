using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class ThoughtAnnouncerTests
    {
        [Fact]
        public void EmptySlot_ReadsStatusAlone()
        {
            var t = new ThoughtSnapshot(null, ThoughtStatusKind.Empty);
            Assert.Equal("empty slot", ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void UnlockableAndLockedSlots_ReadDistinctly()
        {
            Assert.Equal("locked slot, can unlock",
                ThoughtAnnouncer.Compose(new ThoughtSnapshot(null, ThoughtStatusKind.Unlockable)));
            Assert.Equal("locked slot",
                ThoughtAnnouncer.Compose(new ThoughtSnapshot(null, ThoughtStatusKind.Locked)));
        }

        [Fact]
        public void UnknownThought_HidesNameAndReadsStatus()
        {
            var t = new ThoughtSnapshot(null, ThoughtStatusKind.Unknown);
            Assert.Equal("unknown thought", ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void ResearchingThought_ReadsNameProgressEffectsDescription()
        {
            var t = new ThoughtSnapshot("Hardcore", ThoughtStatusKind.Researching, 60,
                "+1 difficulty to all white checks", "A thought about being hardcore.");
            Assert.Equal(
                "Hardcore, researching, 60 percent, +1 difficulty to all white checks, A thought about being hardcore.",
                ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void ResearchedThought_ReadsResearchedStatus()
        {
            var t = new ThoughtSnapshot("Wompty", ThoughtStatusKind.Researched, 0,
                "Learning cap for Logic raised to 4", "The expanded description.");
            Assert.Equal("Wompty, researched, Learning cap for Logic raised to 4, The expanded description.",
                ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void AvailableThought_OmitsMissingEffectsAndDescription()
        {
            var t = new ThoughtSnapshot("Some Thought", ThoughtStatusKind.Available);
            Assert.Equal("Some Thought, available", ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void ResearchingThought_AppendsTimeRemaining()
        {
            var t = new ThoughtSnapshot("Hardcore", ThoughtStatusKind.Researching, 60,
                effects: null, description: null, researchMinutesLeft: 135);
            Assert.Equal("Hardcore, researching, 60 percent, 2 hours 15 minutes remaining",
                ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void AvailableThought_AppendsTotalResearchTime()
        {
            var t = new ThoughtSnapshot("Some Thought", ThoughtStatusKind.Available,
                researchMinutesTotal: 180);
            Assert.Equal("Some Thought, available, research time 3 hours", ThoughtAnnouncer.Compose(t));
        }

        [Fact]
        public void Duration_FormatsHoursAndMinutes()
        {
            Assert.Equal("45 minutes", Strings.Duration(45));
            Assert.Equal("1 hour", Strings.Duration(60));
            Assert.Equal("2 hours 1 minute", Strings.Duration(121));
            Assert.Equal("less than a minute", Strings.Duration(0));
        }

        [Fact]
        public void ComposeStatus_ReturnsStatusWordOnly()
        {
            Assert.Equal("empty slot",
                ThoughtAnnouncer.ComposeStatus(new ThoughtSnapshot(null, ThoughtStatusKind.Empty)));
            Assert.Equal("researching, 0 percent",
                ThoughtAnnouncer.ComposeStatus(new ThoughtSnapshot("X", ThoughtStatusKind.Researching, 0)));
        }
    }
}
