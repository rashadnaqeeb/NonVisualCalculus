using NonVisualCalculus.Core.UI;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class OptionAnnouncerTests
    {
        [Fact]
        public void Toggle_On_ReadsLabelTypeAndStatus()
        {
            var state = OptionState.Toggle("Detective Mode", on: true);
            Assert.Equal("Detective Mode, toggle, on", OptionAnnouncer.Compose(state));
            Assert.Equal("on", OptionAnnouncer.ComposeValue(state));
        }

        [Fact]
        public void Toggle_Off_ReadsOff()
        {
            var state = OptionState.Toggle("Streamer Mode", on: false);
            Assert.Equal("Streamer Mode, toggle, off", OptionAnnouncer.Compose(state));
        }

        [Fact]
        public void Dropdown_ReadsLabelTypeAndCaption()
        {
            var state = OptionState.Dropdown("Display Mode", "Fullscreen");
            Assert.Equal("Display Mode, dropdown, Fullscreen", OptionAnnouncer.Compose(state));
            Assert.Equal("Fullscreen", OptionAnnouncer.ComposeValue(state));
        }

        [Fact]
        public void Dropdown_MissingCaption_OmitsValue()
        {
            var state = OptionState.Dropdown("Resolution", null);
            Assert.Equal("Resolution, dropdown", OptionAnnouncer.Compose(state));
        }

        [Theory]
        [InlineData(0f, "0 percent")]
        [InlineData(0.5f, "50 percent")]
        [InlineData(0.704f, "70 percent")]
        [InlineData(1f, "100 percent")]
        public void ContinuousSlider_ReadsPercentageOfTravel(float fraction, string expectedValue)
        {
            var state = OptionState.ContinuousSlider("Music Volume", fraction);
            Assert.Equal("Music Volume, slider, " + expectedValue, OptionAnnouncer.Compose(state));
            Assert.Equal(expectedValue, OptionAnnouncer.ComposeValue(state));
        }

        [Theory]
        [InlineData(0, "small")]
        [InlineData(1, "medium")]
        [InlineData(2, "large")]
        public void SteppedSlider_MappedToSizeWords(int stepIndex, string expectedValue)
        {
            var state = OptionState.SteppedSlider("Menu Size", SteppedSliderId.MenuSize, stepIndex, stepCount: 3);
            Assert.Equal("Menu Size, slider, " + expectedValue, OptionAnnouncer.Compose(state));
            Assert.Equal(expectedValue, OptionAnnouncer.ComposeValue(state));
        }

        [Fact]
        public void Description_AppendedToFullReadout_NotToValue()
        {
            var state = OptionState.Toggle("Detective Mode", on: false,
                description: "With Detective Mode active, interactable objects are highlighted.");
            Assert.Equal(
                "Detective Mode, toggle, off, With Detective Mode active, interactable objects are highlighted.",
                OptionAnnouncer.Compose(state));
            // The value-only re-announce (on an in-place change) omits the description.
            Assert.Equal("off", OptionAnnouncer.ComposeValue(state));
        }

        [Fact]
        public void Description_Slider_FollowsValue()
        {
            var state = OptionState.SteppedSlider("Menu Size", SteppedSliderId.MenuSize, 1, 3,
                description: "Adjusts the text size of menus");
            Assert.Equal("Menu Size, slider, medium, Adjusts the text size of menus", OptionAnnouncer.Compose(state));
        }

        [Fact]
        public void SteppedSlider_UnknownId_FallsBackToPosition()
        {
            var state = OptionState.SteppedSlider("Mystery", SteppedSliderId.Unknown, stepIndex: 1, stepCount: 4);
            Assert.Equal("Mystery, slider, step 2 of 4", OptionAnnouncer.Compose(state));
        }

        [Fact]
        public void SteppedSlider_IndexOutOfMappedRange_FallsBackToPosition()
        {
            // A mapped slider whose live range exceeds its authored words still reads a sane position.
            var state = OptionState.SteppedSlider("Menu Size", SteppedSliderId.MenuSize, stepIndex: 3, stepCount: 4);
            Assert.Equal("Menu Size, slider, step 4 of 4", OptionAnnouncer.Compose(state));
        }
    }
}
