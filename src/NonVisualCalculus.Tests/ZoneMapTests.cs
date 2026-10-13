using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NonVisualCalculus.Core.World.Zoning;
using Xunit;

namespace NonVisualCalculus.Tests
{
    /// <summary>Synthetic floor plans through the zone segmentation: rooms, doorways, pillars, corridors,
    /// curation, height steps and a staircase. Metres in, zones out.</summary>
    public class ZoneMapTests
    {
        private const float Cell = 0.25f;

        // A raster covering [0, w] by [0, h] metres with a one-cell unwalkable border all round.
        private static WalkRaster Plan(float w, float h)
            => new WalkRaster((int)(w / Cell) + 2, (int)(h / Cell) + 2, Cell, -Cell, -Cell);

        private static void Fill(WalkRaster r, float x0, float z0, float x1, float z1, float elevation = 0f)
        {
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                {
                    float x = r.CenterX(gx), z = r.CenterZ(gz);
                    if (x > x0 && x < x1 && z > z0 && z < z1) r.Set(gx, gz, elevation);
                }
        }

        private static void Clear(WalkRaster r, float x0, float z0, float x1, float z1)
        {
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                {
                    float x = r.CenterX(gx), z = r.CenterZ(gz);
                    if (x > x0 && x < x1 && z > z0 && z < z1) r.Walkable[r.Index(gx, gz)] = false;
                }
        }

        private static ZoneMap Build(WalkRaster r, ZoneCuration? curation = null)
            => ZoneMap.Build(r, new ZoneMapOptions(), curation ?? ZoneCuration.None);

        // Two 8 by 8 m rooms side by side, a 1 m wall between them with a 1 m doorway.
        private static WalkRaster TwoRooms()
        {
            var r = Plan(17f, 8f);
            Fill(r, 0f, 0f, 8f, 8f);
            Fill(r, 9f, 0f, 17f, 8f);
            Fill(r, 8f, 3.5f, 9f, 4.5f); // the doorway
            return r;
        }

        [Fact]
        public void DoorwaySplitsTwoRoomsAndJoinsThemWithOneExitEach()
        {
            var map = Build(TwoRooms());

            Assert.Equal(2, map.Zones.Count);
            var west = map.ZoneAt(new Vector3(4f, 0f, 4f));
            var east = map.ZoneAt(new Vector3(13f, 0f, 4f));
            Assert.NotNull(west);
            Assert.NotNull(east);
            Assert.NotSame(west, east);
            Assert.Single(west!.Exits);
            Assert.Same(east, west.Exits[0].To);
            Assert.Single(east!.Exits);
            Assert.Same(west, east.Exits[0].To);
            // The exit sits in the doorway.
            Assert.InRange(west.Exits[0].Position.X, 7.5f, 9.5f);
            Assert.InRange(west.Exits[0].Position.Z, 3f, 5f);
        }

        [Fact]
        public void ZoneIdsNumberInCentroidOrderFromOne()
        {
            var map = Build(TwoRooms());
            Assert.Equal(new[] { 1, 2 }, map.Zones.Select(z => z.Id).ToArray());
            Assert.Equal(1, map.ZoneAt(new Vector3(4f, 0f, 4f))!.Id); // the western room sorts first on equal Z
        }

        [Fact]
        public void PillarInTheMiddleDoesNotSplitARoom()
        {
            var r = Plan(10f, 10f);
            Fill(r, 0f, 0f, 10f, 10f);
            Clear(r, 4.5f, 4.5f, 5.5f, 5.5f);
            var map = Build(r);
            Assert.Single(map.Zones);
            Assert.Empty(map.Zones[0].Exits);
        }

        [Fact]
        public void ClassWordsFollowAreaAndShape()
        {
            var hall = Plan(20f, 20f); Fill(hall, 0f, 0f, 20f, 20f);
            Assert.Equal(ZoneClass.Hall, Build(hall).Zones.Single().Class);

            var small = Plan(5f, 5f); Fill(small, 0f, 0f, 5f, 5f);
            Assert.Equal(ZoneClass.Small, Build(small).Zones.Single().Class);

            var room = Plan(8f, 8f); Fill(room, 0f, 0f, 8f, 8f);
            Assert.Equal(ZoneClass.Room, Build(room).Zones.Single().Class);

            var passage = Plan(24f, 2f); Fill(passage, 0f, 0f, 24f, 2f);
            Assert.Equal(ZoneClass.Passage, Build(passage).Zones.Single().Class);
        }

        [Fact]
        public void CurationWallForcesASplitWithAnExitAcrossIt()
        {
            var r = Plan(16f, 8f);
            Fill(r, 0f, 0f, 16f, 8f);
            var curation = new ZoneCuration(
                new[] { new CurationWall(new[] { new Vector2(8f, 0f), new Vector2(8f, 8f) }) },
                new Vector3[0][]);
            var map = Build(r, curation);
            Assert.Equal(2, map.Zones.Count);
            var west = map.ZoneAt(new Vector3(3f, 0f, 4f))!;
            Assert.Single(west.Exits);
            Assert.InRange(west.Exits[0].Position.X, 7f, 9f);
        }

        [Fact]
        public void PartitionIdsSplitARoomAlongTheirBoundaryWithAnExit()
        {
            var r = Plan(16f, 8f);
            Fill(r, 0f, 0f, 16f, 8f);
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                    if (r.Walkable[r.Index(gx, gz)]) r.Partition[r.Index(gx, gz)] = r.CenterX(gx) < 8f ? 1 : 2;
            var map = Build(r);
            Assert.Equal(2, map.Zones.Count);
            var west = map.ZoneAt(new Vector3(3f, 0f, 4f))!;
            Assert.Single(west.Exits);
            Assert.InRange(west.Exits[0].Position.X, 7f, 9f);
        }

        [Fact]
        public void ANarrowPartitionedRoomOffAWideOneStaysItsOwnZone()
        {
            // A kitchen: 0.75 m aisles between counters, so nothing in it seeds a basin, joined to the
            // room by an open side; the partition says it is a room of its own.
            var r = Plan(12f, 8f);
            Fill(r, 0f, 0f, 8f, 8f);
            Fill(r, 8f, 2f, 12f, 2.75f);
            Fill(r, 8f, 4f, 12f, 4.75f);
            Fill(r, 11.25f, 2f, 12f, 4.75f);
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                    if (r.Walkable[r.Index(gx, gz)] && r.CenterX(gx) > 8f) r.Partition[r.Index(gx, gz)] = 7;
            var map = Build(r);
            Assert.Equal(2, map.Zones.Count);
            var kitchen = map.ZoneAt(new Vector3(10f, 0f, 2.4f))!;
            Assert.NotSame(kitchen, map.ZoneAt(new Vector3(4f, 0f, 4f)));
            Assert.Contains(kitchen.Exits, e => e.To.Centroid.X < 8f);
        }

        [Fact]
        public void CurationMergeUnionsZonesAcrossADoorway()
        {
            var curation = new ZoneCuration(
                new CurationWall[0],
                new[] { new[] { new Vector3(4f, 0f, 4f), new Vector3(13f, 0f, 4f) } });
            var map = Build(TwoRooms(), curation);
            Assert.Single(map.Zones);
            Assert.Empty(map.Zones[0].Exits);
        }

        [Fact]
        public void CurationDropRemovesAZone()
        {
            var curation = new ZoneCuration(new CurationWall[0], new Vector3[0][], new[] { new Vector3(13f, 0f, 4f) });
            var map = Build(TwoRooms(), curation);
            Assert.Single(map.Zones);
            Assert.Null(map.ZoneAt(new Vector3(13f, 0f, 4f)));
            Assert.Empty(map.Zones[0].Exits);
        }

        [Fact]
        public void HeightStepSeparatesZonesWithoutAnExit()
        {
            var r = Plan(16f, 8f);
            Fill(r, 0f, 0f, 8f, 8f, elevation: 0f);
            Fill(r, 8f, 0f, 16f, 8f, elevation: 2f);
            var map = Build(r);
            Assert.Equal(2, map.Zones.Count);
            Assert.All(map.Zones, z => Assert.Empty(z.Exits));
        }

        [Fact]
        public void ARampBetweenFloorsIsItsOwnStairsZone()
        {
            var r = Plan(20f, 8f);
            Fill(r, 0f, 0f, 8f, 8f, elevation: 0f);
            Fill(r, 12f, 0f, 20f, 8f, elevation: 2f);
            // A 2 m wide ramp climbing 2 m over 4 m (slope 0.5).
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                {
                    float x = r.CenterX(gx), z = r.CenterZ(gz);
                    if (x > 8f && x < 12f && z > 3f && z < 5f) r.Set(gx, gz, (x - 8f) * 0.5f);
                }
            var map = Build(r);
            Assert.Equal(3, map.Zones.Count);
            var stairs = map.Zones.Single(z => z.Class == ZoneClass.Stairs);
            Assert.Equal(2, stairs.Exits.Count);
            Assert.Contains(stairs.Exits, e => e.To.Centroid.X < 8f);
            Assert.Contains(stairs.Exits, e => e.To.Centroid.X > 12f);
        }

        [Fact]
        public void ANarrowWalkwayWithNothingWiderStillGetsAZone()
        {
            // 0.75 m wide: no cell reaches the basin-seeding clearance, so only the leftover pass labels it.
            var r = Plan(12f, 0.75f);
            Fill(r, 0f, 0f, 12f, 0.75f);
            var map = Build(r);
            Assert.Single(map.Zones);
            Assert.NotNull(map.ZoneAt(new Vector3(6f, 0f, 0.4f)));
        }

        [Fact]
        public void AnIsolatedSmallLedgeKeepsItsOwnZone()
        {
            var r = Plan(20f, 8f);
            Fill(r, 0f, 0f, 8f, 8f);
            Fill(r, 15f, 3f, 17f, 5f); // 4 m2, nothing adjoining
            var map = Build(r);
            Assert.Equal(2, map.Zones.Count);
            Assert.NotNull(map.ZoneAt(new Vector3(16f, 0f, 4f)));
        }

        [Fact]
        public void AnIsolatedSpeckUnderASquareMetreIsDropped()
        {
            var r = Plan(12f, 8f);
            Fill(r, 0f, 0f, 8f, 8f);
            Fill(r, 10f, 3f, 10.5f, 3.5f); // a 2 by 2 cell speck, nothing adjoining
            var map = Build(r);
            Assert.Single(map.Zones);
            Assert.Null(map.ZoneAt(new Vector3(10.25f, 0f, 3.25f)));
        }

        [Fact]
        public void TheOnlyGroundStaysAZoneHoweverSmall()
        {
            var r = Plan(4f, 4f);
            Fill(r, 1f, 1f, 1.5f, 1.5f); // a 2 by 2 cell standing spot, nothing else
            var map = Build(r);
            Assert.Single(map.Zones);
            Assert.NotNull(map.ZoneAt(new Vector3(1.25f, 0f, 1.25f)));
        }

        [Fact]
        public void ZoneAtResolvesNearWallsAndRejectsOtherLevelsAndOutside()
        {
            var map = Build(TwoRooms());
            Assert.NotNull(map.ZoneAt(new Vector3(0.1f, 0f, 0.1f)));      // hugging the corner
            Assert.NotNull(map.ZoneAt(new Vector3(-0.2f, 0f, 4f)));        // just past the wall, nearest cell
            Assert.Null(map.ZoneAt(new Vector3(4f, 5f, 4f)));              // another level
            Assert.Null(map.ZoneAt(new Vector3(40f, 0f, 40f)));            // outside the grid
        }

        [Fact]
        public void DumpCarriesHeaderLabelsZonesAndExits()
        {
            var map = Build(TwoRooms());
            string text = ZoneDump.Write(map, "Test-scene");
            var lines = text.Split('\n');
            Assert.Equal($"{map.Raster.Width} {map.Raster.Height} 0.25 -0.25 -0.25 Test-scene", lines[0]);
            Assert.Equal(map.Raster.Count, lines[1].Split(',').Length - 1);
            Assert.Equal(map.Raster.Count, lines[2].Split(',').Length - 1);
            Assert.Equal(2, lines.Count(l => l.StartsWith("Z ")));
            Assert.Equal(2, lines.Count(l => l.StartsWith("E ")));
        }
    }
}
