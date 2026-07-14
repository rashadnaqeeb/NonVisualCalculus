using System.Numerics;
using NonVisualCalculus.Core.World;
using Xunit;

namespace NonVisualCalculus.Tests
{
    public class SpatialReadoutTests
    {
        private static readonly Vector3 Player = new Vector3(0f, 0f, 0f);

        [Fact]
        public void Coincident_ReadsHere()
        {
            Assert.Equal("here", SpatialReadout.Describe(Player, new Vector3(0.01f, 0f, 0f)));
        }

        [Fact]
        public void BearingFirst_ThenDistance()
        {
            // 3 metres due east.
            Assert.Equal("east, 3 meters", SpatialReadout.Describe(Player, new Vector3(3f, 0f, 0f)));
        }

        [Fact]
        public void OneMetre_IsSingular()
        {
            Assert.Equal("north, 1 meter", SpatialReadout.Describe(Player, new Vector3(0f, 0f, 1f)));
        }

        [Fact]
        public void SubMetre_ReadsLessThanAMeter()
        {
            // Past the "here" epsilon but rounds to 0 metres.
            Assert.Equal("north, less than a meter", SpatialReadout.Describe(Player, new Vector3(0f, 0f, 0.3f)));
        }

        [Fact]
        public void VerticalOffset_CountsInDistance_AndTagsDirection()
        {
            // 3 east, 4 up: straight-line distance 5, tagged above (elevation counts toward distance).
            Assert.Equal("east, 5 meters, above", SpatialReadout.Describe(Player, new Vector3(3f, 4f, 0f)));
            Assert.Equal("east, 5 meters, below", SpatialReadout.Describe(Player, new Vector3(3f, -4f, 0f)));
        }

        [Fact]
        public void DirectlyAbove_OmitsBearing()
        {
            // No horizontal offset: distance and the vertical tag, no spurious compass direction.
            Assert.Equal("3 meters, above", SpatialReadout.Describe(Player, new Vector3(0f, 3f, 0f)));
        }
    }
}
