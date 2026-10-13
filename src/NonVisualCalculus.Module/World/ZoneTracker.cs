using System;
using System.Collections.Generic;
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
    /// Rebuilt when the active scene changes, on a retry cooldown while the mesh is not yet answering, and
    /// a moment after an in-place door toggles (doors carve the mesh with a NavMeshObstacle, so walkability
    /// and connectivity change with them; see <see cref="DoorReader"/>). Nothing here is spoken: the
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

        private readonly IModHost _host;
        private string _builtFor;
        private int _retry;
        private float _invalidateAt;
        private bool _settled; // the scene's settle rebuild has been scheduled

        /// <summary>The segmentation thresholds, held here so a dev session can retune and rebuild without
        /// a reload. Disco's values: a shallower persistence dip than WOTR's 0.7 m, chosen on the Whirling's
        /// ground floor (0.7 left the café, its bar and its dining room one zone; 0.5 splits them where the
        /// glass partitions pinch the floor, and 0.4 only fragments the hall) and checked on Martinaise
        /// (129 zones at 0.7, 153 at 0.5, with the plaza and the yards still whole).</summary>
        public ZoneMapOptions Options { get; set; } = new ZoneMapOptions { Persist = 0.5f };

        /// <summary>The scene's authored walls and merges; none until the description store lands.</summary>
        public ZoneCuration Curation { get; set; } = ZoneCuration.None;

        /// <summary>The current scene's zones, or null while none is built.</summary>
        public ZoneMap Map { get; private set; }

        /// <summary>The scene the map belongs to.</summary>
        public string Scene => _builtFor;

        /// <summary>Milliseconds the last build took, sampling included (the dev stats line).</summary>
        public long LastBuildMs { get; private set; }

        public ZoneTracker(IModHost host)
        {
            _host = host;
            Active = this;
        }

        /// <summary>Keep the map current. Call once per in-world frame with the active scene's name and the
        /// character's position (Unity frame): the flood fill seeds there.</summary>
        public void Tick(string scene, Vector3 playerUnity)
        {
            if (_invalidateAt > 0f && Time.unscaledTime >= _invalidateAt)
            {
                _invalidateAt = 0f;
                Map = null;
                _retry = 0;
            }
            if (scene != _builtFor)
            {
                _builtFor = scene;
                Map = null;
                _retry = 0;
                _settled = false;
            }
            if (Map != null || --_retry > 0) return;
            _retry = RetryFrames;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Map = Build(scene, playerUnity);
                LastBuildMs = sw.ElapsedMilliseconds;
                if (Map != null)
                {
                    _host.LogInfo($"ZoneTracker: {scene}: {Map.Zones.Count} zones over {WalkableArea(Map):0} m2 in {LastBuildMs} ms");
                    // The navmesh keeps changing after a scene loads: parked vehicles and props carve
                    // it with NavMeshObstacles over the following frames, and a map built before they
                    // settle joins ground they divide (Martinaise's plaza ran into its road zones). One
                    // rebuild a few seconds after the first build sees the settled mesh.
                    if (!_settled)
                    {
                        _settled = true;
                        _invalidateAt = Time.unscaledTime + LoadSettle;
                    }
                }
            }
            catch (Exception e)
            {
                Map = null;
                _retry = FailureBackoff;
                _host.LogError($"ZoneTracker: building {scene} failed: {e}");
            }
        }

        /// <summary>Schedule a rebuild shortly (a door opened or closed). Repeated toggles push the window out.</summary>
        public void InvalidateSoon() => _invalidateAt = Time.unscaledTime + DoorSettle;

        /// <summary>Drop the map so the next tick rebuilds it (a dev retune).</summary>
        public void Rebuild() { Map = null; _retry = 0; }

        /// <summary>The zone at a point in the mod's world frame, or null.</summary>
        public Zone ZoneAt(Snv p) => Map?.ZoneAt(p);

        private static float WalkableArea(ZoneMap map)
        {
            float a = 0f;
            foreach (var z in map.Zones) a += z.Area;
            return a;
        }

        // The whole raster build. Null when the scene has no renderers to bound it or the character's own
        // spot is not on a mesh yet (the retry cooldown covers both).
        private ZoneMap Build(string scene, Vector3 playerUnity)
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
            var walk = new bool[n];
            var elev = new float[n];
            var tried = new float[n];   // elevation a rejected cell was last sampled from
            var triedAny = new bool[n];
            var queue = new Queue<int>();

            Snv seedCore = WorldConvert.ToSnv(seed.position);
            int sgx = (int)Math.Floor((seedCore.X - x0) / cell), sgz = (int)Math.Floor((seedCore.Z - z0) / cell);
            if (sgx < 0 || sgz < 0 || sgx >= w || sgz >= h) return null;
            int si = sgz * w + sgx;
            walk[si] = true; elev[si] = seed.position.y;
            queue.Enqueue(si);
            Flood(queue, walk, elev, tried, triedAny, w, h, cell, x0, z0, StepReach);

            // Islands the fill could not reach (ground joined only by an off-mesh link, a scripted
            // move, or a door's carve): a coarse sweep seeds each one and floods from it. Sampled at
            // a ladder of elevations around the character's, since an unreached island may be a
            // whole storey up (the Doomed Commercial Area's upper room sits 5 m over its stair).
            for (int gz = 0; gz < h; gz += SeedStride)
                for (int gx = 0; gx < w; gx += SeedStride)
                {
                    int i = gz * w + gx;
                    if (walk[i]) continue;
                    for (float dy = -SeedSpan; dy <= SeedSpan; dy += SeedStep)
                    {
                        if (!TrySample(gx, gz, seed.position.y + dy, SeedReach, cell, x0, z0, out float y)) continue;
                        walk[i] = true; elev[i] = y;
                        queue.Enqueue(i);
                        Flood(queue, walk, elev, tried, triedAny, w, h, cell, x0, z0, StepReach);
                        break;
                    }
                }

            // Crop to the walkable extent plus a one-cell unwalkable border.
            int minX = w, minZ = h, maxX = -1, maxZ = -1;
            for (int i = 0; i < n; i++)
            {
                if (!walk[i]) continue;
                int gx = i % w, gz = i / w;
                if (gx < minX) minX = gx; if (gx > maxX) maxX = gx;
                if (gz < minZ) minZ = gz; if (gz > maxZ) maxZ = gz;
            }
            if (maxX < 0) return null;
            int cw = maxX - minX + 3, ch = maxZ - minZ + 3;
            var raster = new WalkRaster(cw, ch, cell, x0 + (minX - 1) * cell, z0 + (minZ - 1) * cell);
            for (int gz = minZ; gz <= maxZ; gz++)
                for (int gx = minX; gx <= maxX; gx++)
                {
                    int i = gz * w + gx;
                    if (!walk[i]) continue;
                    int rx = gx - minX + 1, rz = gz - minZ + 1;
                    raster.Set(rx, rz, elev[i]);
                    raster.Partition[raster.Index(rx, rz)] = FogRoomAt(raster.CenterX(rx), elev[i], raster.CenterZ(rz));
                }
            return ZoneMap.Build(raster, Options, Curation);
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

        // The fog volumes' physics layer and the reach of the probe, the constants FogSense casts with.
        private const int FogLayerMask = 0x2000;
        private const float FogProbeDistance = 4f;

        // Breadth-first over 4-neighbours: each is sampled at the elevation of the cell it is reached
        // from. A rejected cell may be retried from a neighbour at a meaningfully different elevation.
        private static void Flood(Queue<int> queue, bool[] walk, float[] elev, float[] tried, bool[] triedAny,
                                  int w, int h, float cell, float x0, float z0, float reach)
        {
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int gx = i % w, gz = i / w;
                float e = elev[i];
                for (int k = 0; k < 4; k++)
                {
                    int nx = gx + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = gz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int j = nz * w + nx;
                    if (walk[j]) continue;
                    if (triedAny[j] && Math.Abs(tried[j] - e) < RetrySameElevation) continue;
                    triedAny[j] = true; tried[j] = e;
                    if (!TrySample(nx, nz, e, reach, cell, x0, z0, out float y)) continue;
                    walk[j] = true; elev[j] = y;
                    queue.Enqueue(j);
                }
            }
        }

        // Sample the mesh under a cell centre at a given elevation. A hit counts only when it lies under
        // the centre (within half a cell planar); a hit that snapped sideways is the edge of ground that
        // does not cover this cell.
        private static bool TrySample(int gx, int gz, float atElevation, float reach, float cell, float x0, float z0, out float y)
        {
            y = 0f;
            var core = new Snv(x0 + (gx + 0.5f) * cell, atElevation, z0 + (gz + 0.5f) * cell);
            Vector3 p = WorldConvert.ToUnity(core);
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, reach, NavMesh.AllAreas)) return false;
            float dx = hit.position.x - p.x, dz = hit.position.z - p.z;
            if (dx * dx + dz * dz > cell * cell * 0.25f) return false;
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
