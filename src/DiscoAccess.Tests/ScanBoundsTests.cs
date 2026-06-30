using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.World;
using Xunit;

namespace DiscoAccess.Tests
{
    public class ScanBoundsTests
    {
        [Fact]
        public void Point_NearestIsAlwaysItself()
        {
            var b = ScanBounds.Point(new Vector3(2f, 0f, 3f));
            var np = b.NearestPoint(new Vector3(10f, 0f, 10f));
            Assert.Equal(new Vector3(2f, 0f, 3f), np);
        }

        [Fact]
        public void Circle_NearestIsOnTheRim_TowardTheReference()
        {
            // A radius-1 circle at the origin; from (5,0,0) the nearest point is (1,0,0).
            var b = ScanBounds.Circle(new Vector3(0f, 0f, 0f), 1f);
            var np = b.NearestPoint(new Vector3(5f, 0f, 0f));
            Assert.Equal(1f, np.X, 3);
            Assert.Equal(0f, np.Z, 3);
        }

        [Fact]
        public void Circle_InsideFootprint_ReturnsTheReference()
        {
            var b = ScanBounds.Circle(new Vector3(0f, 0f, 0f), 5f);
            var from = new Vector3(1f, 0f, 1f);
            Assert.Equal(from.X, b.NearestPoint(from).X, 3);
            Assert.Equal(from.Z, b.NearestPoint(from).Z, 3);
        }

        [Fact]
        public void Segments_NearestLiesOnTheClosestEdge()
        {
            // A doorway as one portal edge from (-1,0,0) to (1,0,0); standing due south at (0,0,-5)
            // the nearest point is the edge's midpoint (0,0,0), so the bearing reads south, not to a corner.
            var edge = new List<Vector3> { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            var b = ScanBounds.Segments(new Vector3(0f, 0f, 0f), edge);
            var np = b.NearestPoint(new Vector3(0f, 0f, -5f));
            Assert.Equal(0f, np.X, 3);
            Assert.Equal(0f, np.Z, 3);
        }
    }
}
