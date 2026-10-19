using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.World.Zoning;
using UnityEngine;
using UnityEngine.AI;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The live zone map of the active scene: fills a <see cref="WalkRaster"/> by sampling the navmesh and
    /// hands it to the Core segmentation (<see cref="ZoneMap"/>). The interop exposes no navmesh
    /// triangulation, so walkability is discovered by flood fill: from the character's cell outward, each
    /// neighbour is sampled at the elevation of the cell it was reached from, within a short reach, and a
    /// hit counts only when it lies under the cell centre - so stairs and slopes follow, a ledge's lower
    /// ground is found at its own height, and a mesh edge a cell away never snaps a dead cell alive. A
    /// coarse sweep afterwards seeds any walkable island the fill could not reach. Only the active scene's
    /// mesh is loaded (verified live), so the raster is the scene's floor and nothing else.
    ///
    /// A build never stalls a frame: the navmesh sampling and the fog-room probes (Unity calls, main
    /// thread only) run as a resumable job that spends a few milliseconds per tick, and the segmentation
    /// (pure Core arithmetic) runs on a worker thread; the previous map keeps answering until the new one
    /// lands. Maps are kept per scene for the session, so re-entering a scene answers at once from the
    /// cached map while one quiet rebuild checks it against the mesh. A rebuild is scheduled a moment
    /// after anything that carves the mesh in place: a door toggle (<see cref="DoorReader"/>), a story-state
    /// switch or a Lua blocker toggle (<see cref="ZoneRebuildHooks"/>). Nothing here is spoken: the
    /// consumers read <see cref="Map"/> and resolve zones at speak time.
    /// </summary>
    public sealed class ZoneTracker
    {
        /// <summary>The live tracker, for the door feeder and the dev hooks; null while no world reader.</summary>
        public static ZoneTracker Active;

        private const float DefaultCell = 0.25f;
        private const float MaxCells = 6e6f;      // raster budget before the cell coarsens
        private const float StepReach = 1.0f;     // sample radius around a neighbour's elevation
        private const float SeedReach = 1.4f;     // sample radius for the coarse island sweep (under the ladder step)
        private const float SeedSpan = 15f;       // the sweep's elevation ladder reaches this far above and below the character
        private const float SeedStep = 2.5f;      // rungs of that ladder
        private const int SeedStride = 4;         // cells between coarse sweep samples
        private const float RetrySameElevation = 0.5f; // a rejected cell is retried only from a neighbour this much higher or lower
        private const int RetryFrames = 30;       // between attempts while the mesh is not answering
        private const int FailureBackoff = 300;   // after a throwing build
        private const float DoorSettle = 2f;      // seconds after a door toggle before the rebuild (its carve settles first)
        private const float LoadSettle = 4f;      // seconds after a scene's first build before the one settle rebuild
        private const float BackdropHeight = 100f; // renderers taller than this are backdrop planes, not scene bounds
        private const float BackdropWidth = 2000f;
        private const double FrameBudgetMs = 4.0;  // main-thread work per tick while a job samples
        private const int FogLayerMask = 0x2000;
        private const float FogProbeDistance = 4f;

        private readonly IModHost _host;
        private string _builtFor;
        private int _retry;
        private float _invalidateAt;
        private bool _settled; // the scene's settle rebuild has been scheduled
        private bool _due;     // a scheduled rebuild fired and no job has started for it yet
        private BuildJob _job;
        private readonly Dictionary<string, ZoneMap> _cache = new Dictionary<string, ZoneMap>();
        private readonly Dictionary<string, string> _curationSig = new Dictionary<string, string>();

        /// <summary>The segmentation thresholds, held here so a dev session can retune and rebuild without
        /// a reload. Disco's values: a shallower persistence dip than WOTR's 0.7 m, chosen on the Whirling's
        /// ground floor (0.7 left the café, its bar and its dining room one zone; 0.5 splits them where the
        /// glass partitions pinch the floor, and 0.4 only fragments the hall) and checked on Martinaise
        /// (129 zones at 0.7, 153 at 0.5, with the plaza and the yards still whole).</summary>
        public ZoneMapOptions Options { get; set; } = new ZoneMapOptions { Persist = 0.5f };

        /// <summary>Whether the ground under people's standing spots goes back into the raster (see
        /// CollectFootings); held here so a dev session can compare a scene with and without.</summary>
        public bool FillStandingSpots { get; set; } = true;

        /// <summary>The scene's authored walls and merges; none until the description store lands.</summary>
        public ZoneCuration Curation { get; set; } = ZoneCuration.None;

        /// <summary>The current scene's zones, or null while none is built.</summary>
        public ZoneMap Map { get; private set; }

        /// <summary>Whether the map was built after the scene's obstacles settled (the settle rebuild or a
        /// later one, or a cached map from an earlier visit). The first map of a visit can join ground the
        /// parked vehicles and props divide a few frames later.</summary>
        public bool Settled { get; private set; }

        /// <summary>The scene the map belongs to.</summary>
        public string Scene => _builtFor;

        /// <summary>Dev only: while set, no build starts or resumes and no scheduled rebuild fires, so a
        /// survey's zone ids stay those of the map the scene built on entry (its own state switches and
        /// blocker toggles would otherwise rebuild through the hooks). The survey sets it on its first
        /// Frame or Teleport and clears it in Restore; a scene change clears it too.</summary>
        public bool HoldRebuilds { get; set; }

        /// <summary>Whether a build is in flight (the map, if any, is the previous one meanwhile).</summary>
        public bool Building => _job != null;

        /// <summary>Milliseconds of main-thread work the last build took, spread over its frames.</summary>
        public long LastBuildMs { get; private set; }
        /// <summary>Where the last build's time went (grid size, samples and milliseconds per phase).</summary>
        public string LastBuildBreakdown { get; private set; } = "";

        public ZoneTracker(IModHost host)
        {
            _host = host;
            Active = this;
        }

        /// <summary>Advance the map for the active scene: start, resume or finish a build. Called every
        /// frame from the world reader with the character's position (Unity frame).</summary>
        public void Tick(string scene, Vector3 playerUnity)
        {
            if (scene != _builtFor)
            {
                _builtFor = scene;
                HoldRebuilds = false; // a survey's hold belongs to the scene it was set in
                _job = null;
                _retry = 0;
                _invalidateAt = 0f;
                _due = false;
                // A cached map answers at once; the settle rebuild below then checks it against the live
                // mesh, which may have changed while away (a state set from another area) or be still
                // settling its obstacles.
                _settled = _cache.TryGetValue(scene, out ZoneMap cached);
                Map = cached;
                Settled = _settled;
                if (_settled) _invalidateAt = Time.unscaledTime + LoadSettle;
            }
            if (HoldRebuilds) return;
            if (_invalidateAt > 0f && Time.unscaledTime >= _invalidateAt)
            {
                // A scheduled rebuild is due: the current map keeps answering while the new one builds.
                _invalidateAt = 0f;
                _job = null;
                _retry = 0;
                _due = true;
            }
            if (_job != null) { Advance(_job); return; }
            if (Map != null && !_settled)
            {
                // The map just landed: one rebuild a few seconds on sees the settled mesh. The navmesh
                // keeps changing after a scene loads: parked vehicles and props carve it with
                // NavMeshObstacles over the following frames, and a map built before they settle joins
                // ground they divide (Martinaise's plaza ran into its road zones).
                _settled = true;
                _invalidateAt = Time.unscaledTime + LoadSettle;
                return;
            }
            if (Map != null && !_due) return;
            if (--_retry > 0) return;
            _retry = RetryFrames;
            try
            {
                _job = Start(scene, playerUnity);
                if (_job != null) { _due = false; Advance(_job); }
            }
            catch (Exception e)
            {
                _job = null;
                _retry = FailureBackoff;
                _host.LogError($"ZoneTracker: starting a build of {scene} failed: {e}");
            }
        }

        /// <summary>Schedule a rebuild shortly (a door opened or closed, the story carved the mesh).
        /// Repeated toggles push the window out; the current map keeps answering meanwhile.</summary>
        public void InvalidateSoon() => _invalidateAt = Time.unscaledTime + DoorSettle;

        /// <summary>Take the scene's authored curation (walls, merges, drops) from the description store.
        /// The cached map stands when it was built under the same curation; a different one (a first load
        /// of the scene, an edited file) drops it for a fresh build.</summary>
        public void ApplyCuration(ZoneCuration curation)
        {
            string sig = Signature(curation);
            Curation = curation;
            // A job started this frame (the scene-change tick runs before the store's) carries the
            // previous scene's curation; it is dropped whatever the signature says.
            if (_job != null && !ReferenceEquals(_job.Curation, curation)) { Rebuild(); return; }
            if (_builtFor != null && _curationSig.TryGetValue(_builtFor, out string built) && built == sig) return;
            if (_builtFor != null) _curationSig[_builtFor] = sig;
            Rebuild();
        }

        private static string Signature(ZoneCuration c)
        {
            var sb = new System.Text.StringBuilder();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var w in c.Walls)
            {
                sb.Append('w').Append(w.Elevation.HasValue ? w.Elevation.Value.ToString("0.##", inv) : "-");
                foreach (var pt in w.Points) sb.Append(' ').Append(pt.X.ToString("0.##", inv)).Append(',').Append(pt.Y.ToString("0.##", inv));
            }
            foreach (var m in c.Merges) { sb.Append('m'); foreach (var a in m) sb.Append(a.ToString()); }
            foreach (var d in c.Drops) sb.Append('d').Append(d.ToString());
            foreach (var a in c.Seeds) sb.Append('s').Append(a.ToString());
            return sb.ToString();
        }

        /// <summary>Drop the map and its cache so the next tick builds afresh (a dev retune).</summary>
        public void Rebuild()
        {
            Map = null;
            Settled = false;
            _job = null;
            _retry = 0;
            _invalidateAt = 0f;
            if (_builtFor != null) _cache.Remove(_builtFor);
        }

        /// <summary>The zone at a point in the mod's world frame, or null.</summary>
        public Zone ZoneAt(Snv p) => Map?.ZoneAt(p);

        private static float WalkableArea(ZoneMap map)
        {
            float a = 0f;
            foreach (var z in map.Zones) a += z.Area;
            return a;
        }

        // ---- the resumable build ----

        /// <summary>The ground a person's standing spot carves out of the mesh: a square in the grid's frame
        /// (the mod's world frame) and the elevation the person stands at.</summary>
        private struct Footing { public float X0, Z0, X1, Z1, Y; }

        /// <summary>Any other carving obstacle (a door, a blocker, a parked vehicle) as its world-to-local
        /// transform and its carve's half extents in local units, widened by the agent radius.</summary>
        private struct Carve { public Matrix4x4 ToLocal; public Vector3 Center, Half; }

        private enum Phase { Flood, Sweep, Fill, Segment }

        /// <summary>One build in flight: the grid and the cursors of whichever phase is running, so a
        /// tick can stop at its budget and the next one carry on where it left off.</summary>
        private sealed class BuildJob
        {
            public string Scene;
            public Phase Phase;
            public int W, H;
            public float Cell, X0, Z0, SeedY;
            public bool[] Walk; public float[] Elev; public float[] Tried; public bool[] TriedAny;
            public Queue<int> Queue = new Queue<int>();
            public int SweepGx, SweepGz;                 // the sweep's next point
            public int MinX, MinZ, MaxX, MaxZ;            // the walkable extent, found at the end of the sweep
            public WalkRaster Raster; public int FillGz;  // the fill's next row
            public Task<ZoneMap> Segment;
            public ZoneMapOptions Options; public ZoneCuration Curation;
            public bool AfterSettle;                      // started once the scene's settle rebuild was scheduled
            public List<Footing> Footings = new List<Footing>(); // where people stand, filled back in after the sweep
            public List<Carve> Carves = new List<Carve>();       // every other carve, which a footing never fills
            public int Filled;
            public int Samples, FloodSamples, SweepSamples;
            public long MainMs, FloodMs, SweepMs, FillMs;
            public System.Diagnostics.Stopwatch Wall = System.Diagnostics.Stopwatch.StartNew();
        }

        // Prepare the grid and seed the flood from the character's cell. Null (and a retry) when the
        // scene has no renderers to bound it or the mesh is not answering under the character yet.
        private BuildJob Start(string scene, Vector3 playerUnity)
        {
            if (!SceneBounds(scene, out Bounds b)) return null;
            if (!NavMesh.SamplePosition(playerUnity, out NavMeshHit seed, StepReach, NavMesh.AllAreas)) return null;

            float cell = DefaultCell;
            while ((b.size.x / cell + 3) * (b.size.z / cell + 3) > MaxCells) cell *= 1.5f;
            // The grid lives in the mod's world frame: Core X is Unity -x and Core Z is Unity -z, so the
            // grid's low corner is the Unity bounds' high corner, negated.
            int w = (int)(b.size.x / cell) + 3, h = (int)(b.size.z / cell) + 3;
            float x0 = -b.max.x - cell, z0 = -b.max.z - cell;
            int n = w * h;
            var job = new BuildJob
            {
                Scene = scene, Phase = Phase.Flood, W = w, H = h, Cell = cell, X0 = x0, Z0 = z0, SeedY = seed.position.y,
                Walk = new bool[n], Elev = new float[n], Tried = new float[n], TriedAny = new bool[n],
                Options = (ZoneMapOptions)Options.MemberwiseCloneForBuild(), Curation = Curation, AfterSettle = _settled,
            };
            if (FillStandingSpots) CollectFootings(job);
            Snv seedCore = WorldConvert.ToSnv(seed.position);
            int sgx = (int)Math.Floor((seedCore.X - x0) / cell), sgz = (int)Math.Floor((seedCore.Z - z0) / cell);
            if (sgx < 0 || sgz < 0 || sgx >= w || sgz >= h) return null;
            int si = sgz * w + sgx;
            job.Walk[si] = true; job.Elev[si] = seed.position.y;
            job.Queue.Enqueue(si);
            return job;
        }

        // One tick of work: the main-thread phases run until the frame budget is spent; the segmentation
        // is polled. Finishing lands the map, caches it and logs the build.
        private void Advance(BuildJob job)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                while (sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
                {
                    switch (job.Phase)
                    {
                        case Phase.Flood:
                            if (Flood(job, sw)) { job.FloodMs = job.MainMs + (long)sw.ElapsedMilliseconds; job.FloodSamples = job.Samples; job.Phase = Phase.Sweep; }
                            break;
                        case Phase.Sweep:
                            if (Sweep(job, sw))
                            {
                                job.SweepMs = job.MainMs + (long)sw.ElapsedMilliseconds - job.FloodMs; job.SweepSamples = job.Samples - job.FloodSamples;
                                FillFootings(job);
                                if (!Crop(job)) { Finish(job, null, "no walkable cell"); return; }
                                job.Phase = Phase.Fill;
                            }
                            break;
                        case Phase.Fill:
                            if (Fill(job, sw))
                            {
                                job.FillMs = job.MainMs + (long)sw.ElapsedMilliseconds - job.FloodMs - job.SweepMs;
                                var raster = job.Raster; var options = job.Options; var curation = job.Curation;
                                job.Segment = Task.Run(() => ZoneMap.Build(raster, options, curation));
                                // A build this tracker later abandons is never observed through Advance; its failure must still be logged.
                                var host = _host; string sceneName = job.Scene;
                                job.Segment.ContinueWith(tk => host.LogError($"ZoneTracker: segmenting {sceneName} failed: {tk.Exception?.GetBaseException()}"), TaskContinuationOptions.OnlyOnFaulted);
                                job.Phase = Phase.Segment;
                            }
                            break;
                        case Phase.Segment:
                            if (!job.Segment.IsCompleted) { job.MainMs += (long)sw.ElapsedMilliseconds; return; }
                            if (job.Segment.IsFaulted) throw job.Segment.Exception.GetBaseException();
                            Finish(job, job.Segment.Result, null);
                            return;
                    }
                }
                job.MainMs += (long)sw.ElapsedMilliseconds;
            }
            catch (Exception e)
            {
                _job = null;
                _retry = FailureBackoff;
                _host.LogError($"ZoneTracker: building {job.Scene} failed: {e}");
            }
        }

        private void Finish(BuildJob job, ZoneMap map, string why)
        {
            _job = null;
            if (map == null)
            {
                _retry = RetryFrames;
                _host.LogWarning($"ZoneTracker: {job.Scene}: no map ({why})");
                return;
            }
            LastBuildMs = job.MainMs;
            LastBuildBreakdown = $"grid {job.W}x{job.H}, {job.Filled} cells under {job.Footings.Count} standing spots filled, flood {job.FloodSamples} samples {job.FloodMs} ms, sweep {job.SweepSamples} samples {job.SweepMs} ms, fill {job.FillMs} ms, segment off-thread, {job.Wall.ElapsedMilliseconds} ms wall";
            Map = map;
            Settled = job.AfterSettle;
            _cache[job.Scene] = map;
            _curationSig[job.Scene] = Signature(job.Curation);
            _host.LogInfo($"ZoneTracker: {job.Scene}: {map.Zones.Count} zones over {WalkableArea(map):0} m2, {LastBuildMs} ms on the main thread ({LastBuildBreakdown})");
        }

        // Every character's schedule spot and the party's holding spot carry a small carving obstacle (a
        // GameObject named "Nav Mesh Obstacle", 0.5 m box) so the player paths around whoever stands there.
        // Where the people of this save stand would otherwise decide the zones: a person in a doorway or
        // against a partition narrows it, and the watershed splits the floor differently from one save to
        // the next. Their footprints, widened by the agent radius the carve adds, go back in as ground.
        // Doors, story blockers and parked vehicles are not people and keep their carve.
        private const string PersonObstacle = "Nav Mesh Obstacle";

        private static void CollectFootings(BuildJob job)
        {
            float agent = NavMesh.GetSettingsByIndex(0).agentRadius;
            foreach (var o in UnityEngine.Object.FindObjectsOfType<NavMeshObstacle>())
            {
                if (!o.isActiveAndEnabled || !o.carving) continue;
                Vector3 s = o.transform.lossyScale;
                if (o.gameObject.name != PersonObstacle)
                {
                    // The carve widens by the agent radius in world units; in local units that is over the scale.
                    Vector3 size = o.shape == NavMeshObstacleShape.Capsule ? new Vector3(o.radius * 2f, o.height, o.radius * 2f) : o.size;
                    job.Carves.Add(new Carve
                    {
                        ToLocal = o.transform.worldToLocalMatrix, Center = o.center,
                        Half = new Vector3(size.x * 0.5f + (agent + job.Cell) / Mathf.Max(Mathf.Abs(s.x), 1e-3f), size.y * 0.5f + 1f / Mathf.Max(Mathf.Abs(s.y), 1e-3f),
                                           size.z * 0.5f + (agent + job.Cell) / Mathf.Max(Mathf.Abs(s.z), 1e-3f)),
                    });
                    continue;
                }
                Vector3 c = o.transform.TransformPoint(o.center);
                float half = Mathf.Max(Mathf.Abs(o.size.x * s.x), Mathf.Abs(o.size.z * s.z)) * 0.5f + agent + job.Cell;
                Snv core = WorldConvert.ToSnv(c);
                job.Footings.Add(new Footing { X0 = core.X - half, Z0 = core.Z - half, X1 = core.X + half, Z1 = core.Z + half, Y = c.y });
            }
        }

        private static bool InOtherCarve(BuildJob job, float coreX, float y, float coreZ)
        {
            Vector3 p = WorldConvert.ToUnity(new Snv(coreX, y, coreZ));
            foreach (var c in job.Carves)
            {
                Vector3 l = c.ToLocal.MultiplyPoint3x4(p) - c.Center;
                if (Mathf.Abs(l.x) <= c.Half.x && Mathf.Abs(l.y) <= c.Half.y && Mathf.Abs(l.z) <= c.Half.z) return true;
            }
            return false;
        }

        // Inside a footing, a cell goes back in as ground only where the footing holds ground at the
        // person's level on both sides of it along one axis, and no other obstacle's carve covers it: the
        // hole a person cuts in open floor or in a doorway closes, while a wall, a counter or the blocker
        // beside the person (ground on one side only, or another carve) stays out. Decided on the swept grid before any fill, so nothing cascades. A spot
        // parked off the mesh (the scene origin) holds no ground and adds nothing.
        private static void FillFootings(BuildJob job)
        {
            var fill = new List<int>();
            float gate = job.Options.HeightGate;
            foreach (var f in job.Footings)
            {
                int gx0 = Math.Max(Mathf.FloorToInt((f.X0 - job.X0) / job.Cell), 0), gx1 = Math.Min(Mathf.FloorToInt((f.X1 - job.X0) / job.Cell), job.W - 1);
                int gz0 = Math.Max(Mathf.FloorToInt((f.Z0 - job.Z0) / job.Cell), 0), gz1 = Math.Min(Mathf.FloorToInt((f.Z1 - job.Z0) / job.Cell), job.H - 1);
                bool Ground(int gx, int gz) { int i = gz * job.W + gx; return job.Walk[i] && Math.Abs(job.Elev[i] - f.Y) <= gate; }
                for (int gz = gz0; gz <= gz1; gz++)
                    for (int gx = gx0; gx <= gx1; gx++)
                    {
                        if (job.Walk[gz * job.W + gx]) continue;
                        bool w = false, e = false, s = false, n = false;
                        for (int x = gx - 1; x >= gx0 && !w; x--) w = Ground(x, gz);
                        for (int x = gx + 1; x <= gx1 && !e; x++) e = Ground(x, gz);
                        for (int z = gz - 1; z >= gz0 && !s; z--) s = Ground(gx, z);
                        for (int z = gz + 1; z <= gz1 && !n; z++) n = Ground(gx, z);
                        if (((w && e) || (s && n)) && !InOtherCarve(job, job.X0 + (gx + 0.5f) * job.Cell, f.Y, job.Z0 + (gz + 0.5f) * job.Cell))
                            fill.Add(gz * job.W + gx);
                    }
                foreach (int i in fill) { if (!job.Walk[i]) { job.Walk[i] = true; job.Elev[i] = f.Y; job.Filled++; } }
                fill.Clear();
            }
        }

        // Breadth-first over 4-neighbours: each is sampled at the elevation of the cell it is reached
        // from. A rejected cell may be retried from a neighbour at a meaningfully different elevation.
        // Returns true when the queue has drained.
        private static bool Flood(BuildJob job, System.Diagnostics.Stopwatch sw)
        {
            int w = job.W, h = job.H;
            while (job.Queue.Count > 0)
            {
                int i = job.Queue.Dequeue();
                int gx = i % w, gz = i / w;
                float e = job.Elev[i];
                for (int k = 0; k < 4; k++)
                {
                    int nx = gx + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = gz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int j = nz * w + nx;
                    if (job.Walk[j]) continue;
                    if (job.TriedAny[j] && Math.Abs(job.Tried[j] - e) < RetrySameElevation) continue;
                    job.TriedAny[j] = true; job.Tried[j] = e;
                    if (!TrySample(job, nx, nz, e, StepReach, out float y)) continue;
                    job.Walk[j] = true; job.Elev[j] = y;
                    job.Queue.Enqueue(j);
                }
                if ((job.Samples & 63) == 0 && sw.Elapsed.TotalMilliseconds >= FrameBudgetMs) return false;
            }
            return true;
        }

        // Islands the fill could not reach (ground joined only by an off-mesh link, a scripted move,
        // or a door's carve): a coarse sweep seeds each one and floods from it. Sampled at a ladder of
        // elevations around the character's, since an unreached island may be a whole storey up (the
        // Doomed Commercial Area's upper room sits 5 m over its stair). Returns true when the sweep
        // has covered the grid.
        private static bool Sweep(BuildJob job, System.Diagnostics.Stopwatch sw)
        {
            int w = job.W, h = job.H;
            for (; job.SweepGz < h; job.SweepGz += SeedStride, job.SweepGx = 0)
                for (; job.SweepGx < w; job.SweepGx += SeedStride)
                {
                    int i = job.SweepGz * w + job.SweepGx;
                    if (!job.Walk[i])
                        for (float dy = -SeedSpan; dy <= SeedSpan; dy += SeedStep)
                        {
                            if (!TrySample(job, job.SweepGx, job.SweepGz, job.SeedY + dy, SeedReach, out float y)) continue;
                            job.Walk[i] = true; job.Elev[i] = y;
                            job.Queue.Enqueue(i);
                            // The island's own flood may be large; it resumes across ticks like the first.
                            if (!Flood(job, sw)) return false;
                            break;
                        }
                    if (sw.Elapsed.TotalMilliseconds >= FrameBudgetMs) { job.SweepGx += SeedStride; return false; }
                }
            return job.Queue.Count == 0 || Flood(job, sw);
        }

        // The walkable extent plus a one-cell unwalkable border, as the raster the segmentation reads.
        private static bool Crop(BuildJob job)
        {
            int w = job.W, n = job.W * job.H;
            int minX = w, minZ = job.H, maxX = -1, maxZ = -1;
            for (int i = 0; i < n; i++)
            {
                if (!job.Walk[i]) continue;
                int gx = i % w, gz = i / w;
                if (gx < minX) minX = gx; if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz; if (gz > maxZ) maxZ = gz;
            }
            if (maxX < 0) return false;
            job.MinX = minX; job.MinZ = minZ; job.MaxX = maxX; job.MaxZ = maxZ;
            job.Raster = new WalkRaster(maxX - minX + 3, maxZ - minZ + 3, job.Cell, job.X0 + (minX - 1) * job.Cell, job.Z0 + (minZ - 1) * job.Cell);
            job.FillGz = minZ;
            return true;
        }

        // Copy the walkable cells into the raster with their fog-room partition (a raycast each, so
        // budgeted like the sampling). Returns true when every row is in.
        private static bool Fill(BuildJob job, System.Diagnostics.Stopwatch sw)
        {
            var raster = job.Raster;
            for (; job.FillGz <= job.MaxZ; job.FillGz++)
            {
                int gz = job.FillGz;
                for (int gx = job.MinX; gx <= job.MaxX; gx++)
                {
                    int i = gz * job.W + gx;
                    if (!job.Walk[i]) continue;
                    int rx = gx - job.MinX + 1, rz = gz - job.MinZ + 1;
                    raster.Set(rx, rz, job.Elev[i]);
                    raster.Partition[raster.Index(rx, rz)] = FogRoomAt(raster.CenterX(rx), job.Elev[i], raster.CenterZ(rz));
                }
                if (sw.Elapsed.TotalMilliseconds >= FrameBudgetMs) { job.FillGz++; return false; }
            }
            return true;
        }

        // The game's interior fog volumes (Sunshine.Unseen.Zone, one hand-shaped volume per room) are
        // ready-made room boundaries. Their colliders are unreadable meshes, so membership is asked the
        // way the game asks it (see FogSense): a ray cast straight up from the ground on the fog layer
        // into the volume overhead. The volume's instance id becomes the cell's partition, and the
        // segmentation splits where neighbouring cells answer differently, so a room joined to the next
        // by a wide arch still divides where the designers drew the line. 0 where no volume is overhead
        // (exteriors, corridors).
        private static int FogRoomAt(float coreX, float elevation, float coreZ)
        {
            Vector3 p = WorldConvert.ToUnity(new Snv(coreX, elevation, coreZ));
            if (!Physics.Raycast(p, Vector3.up, out RaycastHit hit, FogProbeDistance, FogLayerMask)) return 0;
            var zone = hit.collider.GetComponent<Sunshine.Unseen.Zone>();
            return zone != null ? zone.GetInstanceID() : 0;
        }

        // Sample the mesh under a cell centre at a given elevation. A hit counts only when it lies under
        // the centre (within half a cell planar); a hit that snapped sideways is the edge of ground that
        // does not cover this cell.
        private static bool TrySample(BuildJob job, int gx, int gz, float atElevation, float reach, out float y)
        {
            y = 0f;
            var core = new Snv(job.X0 + (gx + 0.5f) * job.Cell, atElevation, job.Z0 + (gz + 0.5f) * job.Cell);
            Vector3 p = WorldConvert.ToUnity(core);
            job.Samples++;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, reach, NavMesh.AllAreas)) return false;
            float dx = hit.position.x - p.x, dz = hit.position.z - p.z;
            if (dx * dx + dz * dz > job.Cell * job.Cell * 0.25f) return false;
            y = hit.position.y;
            return true;
        }

        // The active scene's extent from its renderers, dropping backdrop planes whose bounds span the
        // world. Unity frame.
        private static bool SceneBounds(string sceneName, out Bounds bounds)
        {
            bounds = default;
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return false;
            bool any = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds rb = r.bounds;
                    if (rb.size.y > BackdropHeight || rb.size.x > BackdropWidth || rb.size.z > BackdropWidth) continue;
                    if (!any) { bounds = rb; any = true; } else bounds.Encapsulate(rb);
                }
            return any;
        }
    }
}
