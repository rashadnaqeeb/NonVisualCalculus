using System.Numerics;
using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    public class GeoTests
    {
        private static readonly Vector3 Origin = new Vector3(0f, 0f, 0f);

        [Fact]
        public void Distance_IsPlanar_IgnoresHeight()
        {
            // 3-4-5 on XZ, plus a large vertical gap that must not inflate the planar distance.
            var d = Geo.Distance(Origin, new Vector3(3f, 100f, 4f));
            Assert.Equal(5f, d, 3);
        }

        [Theory]
        [InlineData(0f, 1f, 0)]   // +Z = north
        [InlineData(1f, 1f, 1)]   // +X+Z = northeast
        [InlineData(1f, 0f, 2)]   // +X = east
        [InlineData(1f, -1f, 3)]  // +X-Z = southeast
        [InlineData(0f, -1f, 4)]  // -Z = south
        [InlineData(-1f, -1f, 5)] // southwest
        [InlineData(-1f, 0f, 6)]  // -X = west
        [InlineData(-1f, 1f, 7)]  // northwest
        public void CompassIndex_MapsCardinalsAndDiagonals(float x, float z, int expected)
        {
            Assert.Equal(expected, Geo.CompassIndex(Origin, new Vector3(x, 0f, z)));
        }

        [Fact]
        public void CompassIndex_Coincident_IsHere()
        {
            Assert.Equal(-1, Geo.CompassIndex(Origin, new Vector3(0.01f, 0f, -0.02f)));
            Assert.True(Geo.IsHere(Origin, new Vector3(0.01f, 0f, -0.02f)));
        }

        [Theory]
        [InlineData(2f, 1)]    // clearly above
        [InlineData(-2f, -1)]  // clearly below
        [InlineData(1f, 0)]    // within the threshold, level
        [InlineData(-1f, 0)]
        public void VerticalSign_RespectsThreshold(float dy, int expected)
        {
            Assert.Equal(expected, Geo.VerticalSign(Origin, new Vector3(0f, dy, 0f)));
        }
    }
}
