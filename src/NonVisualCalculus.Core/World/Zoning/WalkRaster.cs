using System;

namespace NonVisualCalculus.Core.World.Zoning
{
    /// <summary>
    /// A scene's walkable ground as a flat grid: per cell, whether the character can stand there and the
    /// ground's elevation. The raw input to <see cref="ZoneMap.Build"/>. Row-major, cell (gx, gz) at index
    /// gz * Width + gx, the cell's centre at Origin + (g + 0.5) * Cell on each axis. Coordinates are the
    /// mod's world frame (the frame the cursor, the bookmarks and every readout use), one unit one metre.
    /// Filled by the module from the live navmesh; Core never samples anything itself.
    /// </summary>
    public sealed class WalkRaster
    {
        public int Width { get; }
        public int Height { get; }
        public float Cell { get; }
        public float OriginX { get; }
        public float OriginZ { get; }
        public bool[] Walkable { get; }
        public float[] Elevation { get; }

        /// <summary>An optional per-cell partition id from the scene's own room data (Disco's interior fog
        /// volumes, one per room): 0 where no partition claims the cell. Two walkable neighbours with
        /// different ids are on opposite sides of a hand-placed room boundary, which the segmentation
        /// honours as a wall it cannot cross without an exit.</summary>
        public int[] Partition { get; }

        public WalkRaster(int width, int height, float cell, float originX, float originZ)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "a raster needs at least one cell");
            if (cell <= 0f) throw new ArgumentOutOfRangeException(nameof(cell));
            Width = width;
            Height = height;
            Cell = cell;
            OriginX = originX;
            OriginZ = originZ;
            Walkable = new bool[width * height];
            Elevation = new float[width * height];
            Partition = new int[width * height];
        }

        public int Count => Width * Height;

        public int Index(int gx, int gz) => gz * Width + gx;

        public float CenterX(int gx) => OriginX + (gx + 0.5f) * Cell;

        public float CenterZ(int gz) => OriginZ + (gz + 0.5f) * Cell;

        /// <summary>The cell a world point falls in; false when the point is outside the grid.</summary>
        public bool TryCell(float x, float z, out int gx, out int gz)
        {
            gx = (int)Math.Floor((x - OriginX) / Cell);
            gz = (int)Math.Floor((z - OriginZ) / Cell);
            return gx >= 0 && gz >= 0 && gx < Width && gz < Height;
        }

        /// <summary>Mark a cell walkable at an elevation.</summary>
        public void Set(int gx, int gz, float elevation)
        {
            int i = Index(gx, gz);
            Walkable[i] = true;
            Elevation[i] = elevation;
        }
    }
}
