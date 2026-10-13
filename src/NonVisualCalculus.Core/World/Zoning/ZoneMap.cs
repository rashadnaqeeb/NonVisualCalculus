using System;
using System.Collections.Generic;
using System.Numerics;

namespace NonVisualCalculus.Core.World.Zoning
{
    /// <summary>The shape word a zone gets when it has no authored title yet (spoken through the strings
    /// table by the module; the keys here are never spoken).</summary>
    public enum ZoneClass
    {
        Room,
        Small,
        Hall,
        Passage,
        Corridor,
        Stairs,
    }

    /// <summary>The thresholds the segmentation runs on. Defaults are the WOTR mod's, tuned by eye on its
    /// maps; Disco's values are set from the floor-plan renders (see docs/zones-and-descriptions-plan.md).
    /// Distances in metres, areas in square metres.</summary>
    public sealed class ZoneMapOptions
    {
        /// <summary>How far the clearance must dip between two basins for them to stay separate zones.</summary>
        public float Persist = 0.7f;
        /// <summary>Flat regions smaller than this merge into their largest neighbour.</summary>
        public float MinZoneArea = 12f;
        /// <summary>Cells narrower than this never seed a basin; they are assigned to the nearest zone after.</summary>
        public float CutFloor = 0.45f;
        /// <summary>Rise over run above which a cell is sloped (stairs bake as ramps around 0.6 to 0.8).</summary>
        public float SlopeThreshold = 0.35f;
        /// <summary>The largest height step across which two cells may belong to one zone.</summary>
        public float HeightGate = 0.6f;
        /// <summary>Stair regions smaller than this merge away (a short flight is still a zone).</summary>
        public float MinStairArea = 2.5f;
        /// <summary>A region with no neighbour to merge into stays a zone of its own down to this area; below
        /// it the region is sampling noise (a few cells inside a furniture footprint), not a place.</summary>
        public float MinIslandArea = 1f;
        /// <summary>How far a sloped region must climb to count as stairs rather than a bump in its room.</summary>
        public float StairMinRise = 1.5f;
        /// <summary>Interior obstacle islands up to this area cast no clearance shadow (furniture, pillars).</summary>
        public float FurnitureMax = 12f;
        /// <summary>A point more than this above or below a cell's ground is on another level, not in its zone.</summary>
        public float LevelGap = 3f;
        /// <summary>Area below which a flat zone reads as a small room.</summary>
        public float SmallArea = 35f;
        /// <summary>Area above which a flat zone reads as a hall.</summary>
        public float HallArea = 220f;
        /// <summary>Elongation (major over minor axis) past which a narrow zone is a passage.</summary>
        public float PassageElongation = 2.6f;
        /// <summary>Mean clearance below which an elongated zone is a passage rather than a corridor.</summary>
        public float PassageClearance = 2.2f;
        /// <summary>Elongation past which a wider zone is a corridor.</summary>
        public float CorridorElongation = 3.2f;
        /// <summary>Boundary cells between two zones closer than this belong to one opening.</summary>
        public float ExitClusterDistance = 2f;
        /// <summary>Half-height of the band a curation wall's optional elevation gates it to.</summary>
        public float WallElevationBand = 2.5f;
    }

    /// <summary>One walkable opening between two zones: the midpoint of its boundary cells (the cursor
    /// target), the boundary cells themselves, and the zone it leads into. A zone pair joined by two
    /// separate doorways yields two exits.</summary>
    public sealed class ZoneExit
    {
        public Vector3 Position { get; }
        public IReadOnlyList<Vector3> Boundary { get; }
        public Zone To { get; }

        internal ZoneExit(Vector3 position, IReadOnlyList<Vector3> boundary, Zone to)
        {
            Position = position;
            Boundary = boundary;
            To = to;
        }
    }

    /// <summary>One zone of a scene's walkable ground. Ids are stable for a given raster and curation
    /// (numbered in centroid order), but nothing stores them: authored data anchors to coordinates and
    /// resolves through <see cref="ZoneMap.ZoneAt"/>.</summary>
    public sealed class Zone
    {
        public int Id { get; internal set; }
        public ZoneClass Class { get; }
        public float Area { get; }
        public Vector3 Centroid { get; }
        public List<ZoneExit> Exits { get; } = new List<ZoneExit>();

        internal Zone(ZoneClass cls, float area, Vector3 centroid)
        {
            Class = cls;
            Area = area;
            Centroid = centroid;
        }
    }

    /// <summary>
    /// Splits a scene's walkable ground into zones for orientation, the WOTR mod's room segmentation as pure
    /// grid math. Per cell, clearance is the distance to the nearest wall (a chamfer distance transform);
    /// a persistence watershed grows basins from clearance maxima and keeps two basins apart only where
    /// they meet across a clearance dip of <see cref="ZoneMapOptions.Persist"/> or more (a doorway, a
    /// corridor mouth, a pinch between buildings). Cells never union across a height step or between flat
    /// and sloped ground, so stacked ledges stay apart and a staircase becomes its own zone. Small regions
    /// merge into their largest neighbour, curation walls and merges apply, survivors are numbered by
    /// centroid and classified by area and elongation, and exits are the openings along each pair's
    /// shared boundary. Engine-free and deterministic, so it is unit-tested on synthetic floor plans.
    /// </summary>
    public sealed class ZoneMap
    {
        private static readonly int[] Dz8 = { -1, 1, 0, 0, -1, -1, 1, 1 };
        private static readonly int[] Dx8 = { 0, 0, -1, 1, -1, 1, -1, 1 };

        private readonly int[] _label; // per cell, zone index into Zones, or -1
        private readonly List<Zone> _zones;
        private readonly float _levelGap;

        public WalkRaster Raster { get; }
        public IReadOnlyList<Zone> Zones => _zones;

        /// <summary>Per-cell zone index (into <see cref="Zones"/>), -1 off the walkable ground.</summary>
        public int LabelAt(int gx, int gz) => _label[Raster.Index(gx, gz)];

        private ZoneMap(WalkRaster raster, int[] label, List<Zone> zones, float levelGap)
        {
            Raster = raster;
            _label = label;
            _zones = zones;
            _levelGap = levelGap;
        }

        /// <summary>The zone at a world point, or null off the ground, on another level, or outside the
        /// grid. A direct hit, else the nearest labelled cell within two cells, so a point hugging a wall
        /// or in an unlabelled sliver still resolves to the obvious zone.</summary>
        public Zone? ZoneAt(Vector3 p)
        {
            int l = LabelNear(p.X, p.Y, p.Z);
            return l >= 0 ? _zones[l] : null;
        }

        private int LabelNear(float x, float y, float z)
        {
            int gx = (int)Math.Floor((x - Raster.OriginX) / Raster.Cell);
            int gz = (int)Math.Floor((z - Raster.OriginZ) / Raster.Cell);
            return LabelNear(_label, Raster, gx, gz, y, _levelGap);
        }

        private static int LabelNear(int[] label, WalkRaster r, int gx, int gz, float y, float levelGap)
        {
            for (int ring = 0; ring <= 2; ring++)
                for (int dz = -ring; dz <= ring; dz++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dz), Math.Abs(dx)) != ring) continue;
                        int nz = gz + dz, nx = gx + dx;
                        if (nz < 0 || nx < 0 || nz >= r.Height || nx >= r.Width) continue;
                        int i = r.Index(nx, nz);
                        if (label[i] < 0) continue;
                        if (Math.Abs(r.Elevation[i] - y) > levelGap) continue;
                        return label[i];
                    }
            return -1;
        }

        public static ZoneMap Build(WalkRaster raster, ZoneMapOptions options, ZoneCuration curation)
        {
            int w = raster.Width, h = raster.Height, n = w * h;
            float cell = raster.Cell;
            bool[] walk = raster.Walkable;
            float[] elev = raster.Elevation;

            // 1) Interior obstacle islands: unwalkable blobs fully surrounded by walkable ground, up to
            //    FurnitureMax, cast no clearance shadow, so a table or a pillar row does not split its room.
            //    Anything touching the grid border is structure.
            bool[] noShadow = FurnitureIslands(walk, w, h, cell, options.FurnitureMax);

            // 2) Clearance: chamfer 3-4 distance transform to the nearest wall, in metres. Curation walls
            //    seed zero clearance along their polylines first.
            float[] clear = Clearance(walk, noShadow, elev, raster, curation, options);

            // 3) Slope mask: cells on a sustained gradient, closed then opened so landings and specks
            //    do not fragment a staircase.
            bool[] sloped = SlopeMask(walk, elev, w, h, cell, options.SlopeThreshold);

            // 4) Persistence watershed: visit cells by descending clearance; basins meeting across a
            //    saddle merge unless both rise at least Persist above it. Never across a height step or a
            //    flat/sloped change; sloped basins always merge (one staircase is one zone).
            int[] parent = new int[n];
            float[] peak = new float[n];
            bool[] seen = new bool[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int[] order = new int[n];
            float[] keys = new float[n];
            for (int i = 0; i < n; i++) { order[i] = i; keys[i] = -clear[i]; }
            Array.Sort(keys, order);
            for (int oi = 0; oi < n; oi++)
            {
                int i = order[oi];
                float c = clear[i];
                if (c < options.CutFloor) break;
                if (!walk[i]) continue;
                seen[i] = true;
                peak[i] = c;
                int gz = i / w, gx = i % w;
                int me = Find(parent, i);
                for (int k = 0; k < 8; k++)
                {
                    int nz = gz + Dz8[k], nx = gx + Dx8[k];
                    if (nz < 0 || nx < 0 || nz >= h || nx >= w) continue;
                    int j = nz * w + nx;
                    if (!seen[j]) continue;
                    if (sloped[j] != sloped[i] || Math.Abs(elev[j] - elev[i]) > options.HeightGate) continue;
                    int r = Find(parent, j);
                    if (r == me) continue;
                    if (sloped[i] || Math.Min(peak[r], peak[me]) - c < options.Persist)
                    {
                        float pk = Math.Max(peak[r], peak[me]);
                        parent[me] = r;
                        me = r;
                        peak[r] = pk;
                    }
                }
            }

            // 5) Label basins, then flood the sub-CutFloor walkable slivers to the nearest region
            //    (8-connected, height-gated, never across a partition line), so tight offshoots still
            //    report a zone.
            int[] part = raster.Partition;
            int[] label = new int[n];
            var regionOf = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) label[i] = -1;
            for (int i = 0; i < n; i++)
            {
                if (!seen[i]) continue;
                int r = Find(parent, i);
                if (!regionOf.TryGetValue(r, out int id)) { id = regionOf.Count; regionOf[r] = id; }
                label[i] = id;
            }
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++) if (label[i] >= 0) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int gz = i / w, gx = i % w;
                for (int k = 0; k < 8; k++)
                {
                    int nz = gz + Dz8[k], nx = gx + Dx8[k];
                    if (nz < 0 || nx < 0 || nz >= h || nx >= w) continue;
                    int j = nz * w + nx;
                    if (walk[j] && label[j] < 0 && part[j] == part[i] && Math.Abs(elev[j] - elev[i]) <= options.HeightGate)
                    {
                        label[j] = label[i];
                        queue.Enqueue(j);
                    }
                }
            }

            // 5b) Ground no basin reached: a walkway narrower than twice CutFloor has no cell wide enough
            //     to seed a basin, and if nothing wider adjoins it on its own side of a partition line
            //     the flood above never touches it (the Doomed Commercial Area's upper balcony, the
            //     Whirling's kitchen between its counters). Every walkable cell belongs somewhere, so each
            //     leftover connected component becomes a region of its own.
            int regions = regionOf.Count;
            for (int i = 0; i < n; i++)
            {
                if (!walk[i] || label[i] >= 0) continue;
                int id = regions++;
                label[i] = id;
                queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    int gz = c / w, gx = c % w;
                    for (int k = 0; k < 8; k++)
                    {
                        int nz = gz + Dz8[k], nx = gx + Dx8[k];
                        if (nz < 0 || nx < 0 || nz >= h || nx >= w) continue;
                        int j = nz * w + nx;
                        if (walk[j] && label[j] < 0 && part[j] == part[c] && Math.Abs(elev[j] - elev[c]) <= options.HeightGate)
                        {
                            label[j] = id;
                            queue.Enqueue(j);
                        }
                    }
                }
            }

            // 6) Merge small regions into a neighbour: a smaller floor for stairs, borders counted only
            //    where the height is continuous and on the same side of any partition line (a small room
            //    the scene's own data marks as a room stays one), same-class neighbours preferred. A
            //    small region with no neighbour at all stays a zone: a ledge or landing is still ground
            //    the character can stand on, and "no zone" there would be a silent hole.
            int[] size = new int[regions];
            int[] slopedCells = new int[regions];
            float[] minY = new float[regions];
            float[] maxY = new float[regions];
            for (int r = 0; r < regions; r++) { minY[r] = float.MaxValue; maxY[r] = float.MinValue; }
            for (int i = 0; i < n; i++)
            {
                int l = label[i];
                if (l < 0) continue;
                size[l]++;
                if (sloped[i]) slopedCells[l]++;
                if (elev[i] < minY[l]) minY[l] = elev[i];
                if (elev[i] > maxY[l]) maxY[l] = elev[i];
            }
            bool IsStair(int r) => slopedCells[r] * 2 > size[r] && maxY[r] - minY[r] >= options.StairMinRise;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int rid = 0; rid < regions; rid++)
                {
                    if (size[rid] == 0) continue;
                    float minArea = IsStair(rid) ? options.MinStairArea : options.MinZoneArea;
                    if (size[rid] * cell * cell >= minArea) continue;
                    var border = new Dictionary<int, int>();
                    for (int i = 0; i < n; i++)
                    {
                        if (label[i] != rid) continue;
                        int gz = i / w, gx = i % w;
                        for (int k = 0; k < 8; k++)
                        {
                            int nz = gz + Dz8[k], nx = gx + Dx8[k];
                            if (nz < 0 || nx < 0 || nz >= h || nx >= w) continue;
                            int j = nz * w + nx;
                            int l = label[j];
                            if (l >= 0 && l != rid && part[j] == part[i] && Math.Abs(elev[j] - elev[i]) <= options.HeightGate)
                            {
                                border.TryGetValue(l, out int cnt);
                                border[l] = cnt + 1;
                            }
                        }
                    }
                    int target = -1, best = 0;
                    foreach (var kv in border)
                        if (IsStair(kv.Key) == IsStair(rid) && kv.Value > best) { best = kv.Value; target = kv.Key; }
                    if (target < 0)
                        foreach (var kv in border)
                            if (kv.Value > best) { best = kv.Value; target = kv.Key; }
                    if (target < 0 && size[rid] * cell * cell >= options.MinIslandArea) continue;
                    for (int i = 0; i < n; i++) if (label[i] == rid) label[i] = target; // -1 drops a speck
                    if (target < 0) { size[rid] = 0; changed = true; continue; }
                    size[target] += size[rid];
                    slopedCells[target] += slopedCells[rid];
                    if (minY[rid] < minY[target]) minY[target] = minY[rid];
                    if (maxY[rid] > maxY[target]) maxY[target] = maxY[rid];
                    size[rid] = 0;
                    changed = true;
                }
            }

            // 7) Curated merges: the regions holding a group's anchors become one.
            foreach (var group in curation.Merges)
            {
                if (group == null || group.Count < 2) continue;
                int target = -1;
                var members = new List<int>();
                foreach (var a in group)
                {
                    int gx = (int)Math.Floor((a.X - raster.OriginX) / cell);
                    int gz = (int)Math.Floor((a.Z - raster.OriginZ) / cell);
                    int l = LabelNear(label, raster, gx, gz, a.Y, options.LevelGap);
                    if (l < 0) continue;
                    if (target < 0) target = l;
                    else if (l != target && !members.Contains(l)) members.Add(l);
                }
                if (target < 0 || members.Count == 0) continue;
                for (int i = 0; i < n; i++) if (members.Contains(label[i])) label[i] = target;
            }

            // 7b) Curated drops: the region holding each anchor leaves the map.
            foreach (var a in curation.Drops)
            {
                int gx = (int)Math.Floor((a.X - raster.OriginX) / cell);
                int gz = (int)Math.Floor((a.Z - raster.OriginZ) / cell);
                int l = LabelNear(label, raster, gx, gz, a.Y, options.LevelGap);
                if (l < 0) continue;
                for (int i = 0; i < n; i++) if (label[i] == l) label[i] = -1;
            }

            // 8) Stable numbering (centroid order) and classification.
            var cellsOf = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                if (label[i] < 0) continue;
                if (!cellsOf.TryGetValue(label[i], out var list)) { list = new List<int>(); cellsOf[label[i]] = list; }
                list.Add(i);
            }
            var infos = new List<KeyValuePair<int, Zone>>();
            foreach (var kv in cellsOf)
            {
                var cells = kv.Value;
                int cnt = cells.Count;
                double sx = 0, sy = 0, sz = 0, sc = 0;
                foreach (var i in cells) { sx += i % w; sz += i / w; sy += elev[i]; sc += clear[i]; }
                float mx = (float)(sx / cnt), mz = (float)(sz / cnt);
                double cxx = 0, czz = 0, cxz = 0;
                foreach (var i in cells)
                {
                    double ddx = i % w - mx, ddz = i / w - mz;
                    cxx += ddx * ddx; czz += ddz * ddz; cxz += ddx * ddz;
                }
                cxx /= cnt; czz /= cnt; cxz /= cnt;
                double tr = cxx + czz, det = cxx * czz - cxz * cxz;
                double disc = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
                double e1 = tr / 2 + disc, e2 = Math.Max(tr / 2 - disc, 1e-6);
                float elong = (float)Math.Sqrt(e1 / e2);
                float area = cnt * cell * cell;
                float meanClear = (float)(sc / cnt);
                ZoneClass cls;
                if (IsStair(kv.Key)) cls = ZoneClass.Stairs;
                else if (elong > options.PassageElongation && meanClear < options.PassageClearance) cls = ZoneClass.Passage;
                else if (elong > options.CorridorElongation) cls = ZoneClass.Corridor;
                else if (area < options.SmallArea) cls = ZoneClass.Small;
                else if (area > options.HallArea) cls = ZoneClass.Hall;
                else cls = ZoneClass.Room;
                var centroid = new Vector3(raster.OriginX + (mx + 0.5f) * cell, (float)(sy / cnt), raster.OriginZ + (mz + 0.5f) * cell);
                infos.Add(new KeyValuePair<int, Zone>(kv.Key, new Zone(cls, area, centroid)));
            }
            infos.Sort((a, b) =>
            {
                int c = a.Value.Centroid.Z.CompareTo(b.Value.Centroid.Z);
                return c != 0 ? c : a.Value.Centroid.X.CompareTo(b.Value.Centroid.X);
            });
            var zones = new List<Zone>();
            var remap = new Dictionary<int, int>();
            for (int k = 0; k < infos.Count; k++)
            {
                infos[k].Value.Id = k + 1;
                remap[infos[k].Key] = k;
                zones.Add(infos[k].Value);
            }
            for (int i = 0; i < n; i++)
                label[i] = label[i] >= 0 && remap.TryGetValue(label[i], out int z) ? z : -1;

            var map = new ZoneMap(raster, label, zones, options.LevelGap);
            map.BuildExits(options);
            return map;
        }

        private static int Find(int[] parent, int a)
        {
            while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; }
            return a;
        }

        private static bool[] FurnitureIslands(bool[] walk, int w, int h, float cell, float furnitureMax)
        {
            int n = w * h;
            var noShadow = new bool[n];
            var visited = new bool[n];
            var stack = new Stack<int>();
            var cells = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (walk[i] || visited[i]) continue;
                cells.Clear();
                bool touchesBorder = false;
                visited[i] = true;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int j = stack.Pop();
                    cells.Add(j);
                    int gz = j / w, gx = j % w;
                    if (gz == 0 || gx == 0 || gz == h - 1 || gx == w - 1) touchesBorder = true;
                    for (int k = 0; k < 8; k++)
                    {
                        int nz = gz + Dz8[k], nx = gx + Dx8[k];
                        if (nz < 0 || nx < 0 || nz >= h || nx >= w) continue;
                        int m = nz * w + nx;
                        if (!walk[m] && !visited[m]) { visited[m] = true; stack.Push(m); }
                    }
                }
                if (!touchesBorder && cells.Count * cell * cell <= furnitureMax)
                    foreach (var j in cells) noShadow[j] = true;
            }
            return noShadow;
        }

        private static float[] Clearance(bool[] walk, bool[] noShadow, float[] elev, WalkRaster r,
                                         ZoneCuration curation, ZoneMapOptions options)
        {
            int w = r.Width, h = r.Height, n = w * h;
            float cell = r.Cell;
            const int Inf = int.MaxValue / 4;
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = (walk[i] || noShadow[i]) ? Inf : 0;
            foreach (var wall in curation.Walls)
            {
                if (wall?.Points == null || wall.Points.Count < 2) continue;
                for (int s = 0; s + 1 < wall.Points.Count; s++)
                {
                    Vector2 p1 = wall.Points[s], p2 = wall.Points[s + 1];
                    float len = Vector2.Distance(p1, p2);
                    int steps = Math.Max(1, (int)Math.Ceiling(len / (cell * 0.5f)));
                    for (int st = 0; st <= steps; st++)
                    {
                        float t = (float)st / steps;
                        float x = p1.X + (p2.X - p1.X) * t, z = p1.Y + (p2.Y - p1.Y) * t;
                        if (!r.TryCell(x, z, out int gx, out int gz)) continue;
                        int ci = r.Index(gx, gz);
                        if (wall.Elevation.HasValue && walk[ci] && Math.Abs(elev[ci] - wall.Elevation.Value) > options.WallElevationBand) continue;
                        dist[ci] = 0;
                    }
                }
            }
            // Partition boundaries from the scene's own room data: a walkable cell whose 4-neighbour
            // answers a different partition sits on a hand-placed room line, and reads as zero clearance
            // on both sides of it.
            int[] part = r.Partition;
            for (int gz = 0; gz < h; gz++)
                for (int gx = 0; gx < w; gx++)
                {
                    int i = gz * w + gx;
                    if (!walk[i]) continue;
                    int p = part[i];
                    if ((gx > 0 && walk[i - 1] && part[i - 1] != p) || (gx < w - 1 && walk[i + 1] && part[i + 1] != p)
                        || (gz > 0 && walk[i - w] && part[i - w] != p) || (gz < h - 1 && walk[i + w] && part[i + w] != p))
                        dist[i] = 0;
                }
            for (int gz = 0; gz < h; gz++)
                for (int gx = 0; gx < w; gx++)
                {
                    int i = gz * w + gx;
                    if (dist[i] == 0) continue;
                    int best = dist[i];
                    if (gx > 0) best = Math.Min(best, dist[i - 1] + 3);
                    if (gz > 0)
                    {
                        best = Math.Min(best, dist[i - w] + 3);
                        if (gx > 0) best = Math.Min(best, dist[i - w - 1] + 4);
                        if (gx < w - 1) best = Math.Min(best, dist[i - w + 1] + 4);
                    }
                    dist[i] = best;
                }
            for (int gz = h - 1; gz >= 0; gz--)
                for (int gx = w - 1; gx >= 0; gx--)
                {
                    int i = gz * w + gx;
                    if (dist[i] == 0) continue;
                    int best = dist[i];
                    if (gx < w - 1) best = Math.Min(best, dist[i + 1] + 3);
                    if (gz < h - 1)
                    {
                        best = Math.Min(best, dist[i + w] + 3);
                        if (gx < w - 1) best = Math.Min(best, dist[i + w + 1] + 4);
                        if (gx > 0) best = Math.Min(best, dist[i + w - 1] + 4);
                    }
                    dist[i] = best;
                }
            var clear = new float[n];
            for (int i = 0; i < n; i++) clear[i] = dist[i] * (cell / 3f);
            return clear;
        }

        private static bool[] SlopeMask(bool[] walk, float[] elev, int w, int h, float cell, float slopeThreshold)
        {
            int n = w * h;
            var sloped = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!walk[i]) continue;
                int gz = i / w, gx = i % w;
                float dy = 0f;
                if (gx > 0 && walk[i - 1]) dy = Math.Max(dy, Math.Abs(elev[i] - elev[i - 1]));
                if (gx < w - 1 && walk[i + 1]) dy = Math.Max(dy, Math.Abs(elev[i] - elev[i + 1]));
                if (gz > 0 && walk[i - w]) dy = Math.Max(dy, Math.Abs(elev[i] - elev[i - w]));
                if (gz < h - 1 && walk[i + w]) dy = Math.Max(dy, Math.Abs(elev[i] - elev[i + w]));
                sloped[i] = dy / cell > slopeThreshold;
            }
            Morph(sloped, w, h, 2, dilate: true);
            Morph(sloped, w, h, 2, dilate: false);
            for (int i = 0; i < n; i++) sloped[i] &= walk[i];
            Morph(sloped, w, h, 1, dilate: false);
            Morph(sloped, w, h, 1, dilate: true);
            for (int i = 0; i < n; i++) sloped[i] &= walk[i];
            return sloped;
        }

        // 4-neighbourhood binary dilation or erosion passes.
        private static void Morph(bool[] m, int w, int h, int iters, bool dilate)
        {
            int n = w * h;
            var src = new bool[n];
            for (int it = 0; it < iters; it++)
            {
                Array.Copy(m, src, n);
                for (int i = 0; i < n; i++)
                {
                    int gz = i / w, gx = i % w;
                    if (dilate)
                        m[i] = src[i] || (gx > 0 && src[i - 1]) || (gx < w - 1 && src[i + 1])
                            || (gz > 0 && src[i - w]) || (gz < h - 1 && src[i + w]);
                    else
                        m[i] = src[i] && (gx == 0 || src[i - 1]) && (gx == w - 1 || src[i + 1])
                            && (gz == 0 || src[i - w]) && (gz == h - 1 || src[i + w]);
                }
            }
        }

        // Exits: every height-continuous cell edge where one zone's cell meets another's is a boundary
        // point; per zone pair the points cluster (single link, ExitClusterDistance) into openings, one
        // exit on both zones each. The exit position is the opening's midpoint snapped to its nearest
        // boundary point, so it always lies on the threshold itself.
        private void BuildExits(ZoneMapOptions options)
        {
            var r = Raster;
            int w = r.Width, h = r.Height;
            var boundary = new Dictionary<long, List<Vector3>>();
            for (int gz = 0; gz < h; gz++)
                for (int gx = 0; gx < w; gx++)
                {
                    int i = r.Index(gx, gz);
                    if (_label[i] < 0) continue;
                    if (gx + 1 < w) AddBoundary(boundary, i, gx, gz, r.Index(gx + 1, gz), gx + 1, gz, options);
                    if (gz + 1 < h) AddBoundary(boundary, i, gx, gz, r.Index(gx, gz + 1), gx, gz + 1, options);
                }
            float link2 = options.ExitClusterDistance * options.ExitClusterDistance;
            foreach (var kv in boundary)
            {
                var a = _zones[(int)(kv.Key >> 32)];
                var b = _zones[(int)(kv.Key & 0xFFFFFFFF)];
                var pts = kv.Value;
                var root = new int[pts.Count];
                for (int i = 0; i < root.Length; i++) root[i] = i;
                for (int i = 0; i < pts.Count; i++)
                    for (int j = i + 1; j < pts.Count; j++)
                    {
                        float dx = pts[i].X - pts[j].X, dz = pts[i].Z - pts[j].Z;
                        if (dx * dx + dz * dz > link2) continue;
                        int ri = Find(root, i), rj = Find(root, j);
                        if (ri != rj) root[ri] = rj;
                    }
                var groups = new Dictionary<int, List<Vector3>>();
                for (int i = 0; i < pts.Count; i++)
                {
                    int g = Find(root, i);
                    if (!groups.TryGetValue(g, out var list)) { list = new List<Vector3>(); groups[g] = list; }
                    list.Add(pts[i]);
                }
                foreach (var g in groups.Values)
                {
                    Vector3 sum = Vector3.Zero;
                    foreach (var p in g) sum += p;
                    Vector3 mid = sum / g.Count;
                    Vector3 pos = g[0];
                    float best = float.MaxValue;
                    foreach (var p in g)
                    {
                        float d = Vector3.DistanceSquared(p, mid);
                        if (d < best) { best = d; pos = p; }
                    }
                    var cells = g.ToArray();
                    a.Exits.Add(new ZoneExit(pos, cells, b));
                    b.Exits.Add(new ZoneExit(pos, cells, a));
                }
            }
        }

        private void AddBoundary(Dictionary<long, List<Vector3>> map, int i, int gxi, int gzi, int j, int gxj, int gzj, ZoneMapOptions options)
        {
            int la = _label[i], lb = _label[j];
            if (lb < 0 || lb == la) return;
            var r = Raster;
            if (Math.Abs(r.Elevation[i] - r.Elevation[j]) > options.HeightGate) return;
            long key = ((long)Math.Min(la, lb) << 32) | (uint)Math.Max(la, lb);
            if (!map.TryGetValue(key, out var pts)) { pts = new List<Vector3>(); map[key] = pts; }
            float x = r.OriginX + ((gxi + gxj) * 0.5f + 0.5f) * r.Cell;
            float z = r.OriginZ + ((gzi + gzj) * 0.5f + 0.5f) * r.Cell;
            pts.Add(new Vector3(x, (r.Elevation[i] + r.Elevation[j]) * 0.5f, z));
        }
    }
}
