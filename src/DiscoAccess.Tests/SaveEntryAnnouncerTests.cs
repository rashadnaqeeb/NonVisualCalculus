using DiscoAccess.Core.UI;
using Xunit;

namespace DiscoAccess.Tests
{
    public class SaveEntryAnnouncerTests
    {
        [Fact]
        public void Compose_OrdersNameDateThenTime()
        {
            var state = new SaveEntryState("QuickSave", "08 OCT '25", "19:31");
            Assert.Equal("QuickSave, 08 OCT '25, 19:31", SaveEntryAnnouncer.Compose(state));
        }

        [Fact]
        public void Compose_NoTimestamp_ReadsNameOnly()
        {
            var state = new SaveEntryState("New Save", date: null, time: null);
            Assert.Equal("New Save", SaveEntryAnnouncer.Compose(state));
        }
    }
}
