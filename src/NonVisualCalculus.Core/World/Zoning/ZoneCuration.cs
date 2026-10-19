using System.Collections.Generic;
using System.Numerics;

namespace NonVisualCalculus.Core.World.Zoning
{
    /// <summary>
    /// A hand-drawn virtual wall: a polyline on the ground that the segmentation treats as zero
    /// clearance, so the watershed is forced to split along it. Cells stay walkable (the ground still
    /// connects, so an exit forms across the line); only the clearance field dips. The optional
    /// elevation gates the wall to one floor, within 2.5 m, where a line would otherwise cut ground
    /// stacked above or below it.
    /// </summary>
    public sealed class CurationWall
    {
        public IReadOnlyList<Vector2> Points { get; }
        public float? Elevation { get; }

        public CurationWall(IReadOnlyList<Vector2> points, float? elevation = null)
        {
            Points = points;
            Elevation = elevation;
        }
    }

    /// <summary>
    /// The judgment half of a scene's zoning: walls that force splits the clearance heuristic misses
    /// (a doorway the painting shows but the mesh does not pinch, a square that honestly holds two
    /// places) and merge groups that undo splits it invents (a hall carved by a pillar row). Every
    /// entry is a world coordinate, never a zone id, so the data survives re-segmentation: a merge
    /// group unions whichever zones its anchors resolve to at build time.
    /// </summary>
    public sealed class ZoneCuration
    {
        public static readonly ZoneCuration None = new ZoneCuration(new CurationWall[0], new Vector3[0][], new Vector3[0], new Vector3[0]);

        public IReadOnlyList<CurationWall> Walls { get; }
        public IReadOnlyList<IReadOnlyList<Vector3>> Merges { get; }
        /// <summary>Anchors whose zone is removed from the map: navmesh the character can never stand on
        /// (a stray patch at the scene origin where the game parks disabled things).</summary>
        public IReadOnlyList<Vector3> Drops { get; }
        /// <summary>The named zones' anchors. When any are given they steer the segmentation's last step:
        /// a region holding several is divided between them by walking distance, and a region holding none
        /// joins a named neighbour, so every name has its zone and no ground speaks a fallback whatever
        /// the save's doors, vehicles and people do to the watershed.</summary>
        public IReadOnlyList<Vector3> Seeds { get; }

        public ZoneCuration(IReadOnlyList<CurationWall> walls, IReadOnlyList<IReadOnlyList<Vector3>> merges)
            : this(walls, merges, new Vector3[0], new Vector3[0]) { }

        public ZoneCuration(IReadOnlyList<CurationWall> walls, IReadOnlyList<IReadOnlyList<Vector3>> merges, IReadOnlyList<Vector3> drops)
            : this(walls, merges, drops, new Vector3[0]) { }

        public ZoneCuration(IReadOnlyList<CurationWall> walls, IReadOnlyList<IReadOnlyList<Vector3>> merges,
                            IReadOnlyList<Vector3> drops, IReadOnlyList<Vector3> seeds)
        {
            Walls = walls;
            Merges = merges;
            Drops = drops;
            Seeds = seeds;
        }
    }
}
