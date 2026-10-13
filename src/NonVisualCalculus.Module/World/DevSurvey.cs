using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using FortressOccident;
using NonVisualCalculus.Core.World;
using NonVisualCalculus.Core.World.Zoning;
using UnityEngine;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The survey half of the description-authoring pipeline (docs/zones-and-descriptions-plan.md):
    /// tools/survey.py drives these through the dev server's /eval, so the logic lives here,
    /// compile-checked, with the mod's own zone map, registry and environment at hand. Every method
    /// returns JSON. Coordinates in and out are the mod's world frame; the camera and the game are
    /// addressed in Unity's frame internally. <see cref="Frame"/> saves the camera, HUD and fog state on
    /// first use and <see cref="Restore"/> puts everything back; the zoom pin is suspended in between.
    /// Dev tooling only: nothing here runs in play or is spoken.
    /// </summary>
    public static class DevSurvey
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        private static WorldReader Reader => WorldReader.Active;
        private static ZoneTracker Zones => ZoneTracker.Active;

        // ---- read-only queries ----

        public static string Status()
        {
            var p = Party.Player.Main;
            Snv pos = p != null ? WorldConvert.ToSnv(p.transform.position) : default;
            return Ser(new
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                player = new { x = R(pos.X), y = R(pos.Y), z = R(pos.Z) },
                zonesReady = Zones?.Map != null,
                zones = Zones?.Map?.Zones.Count ?? 0,
                screen = new { w = Screen.width, h = Screen.height },
            });
        }

        /// <summary>Every zone with survey spots: the centroid plus farthest-point samples over its own
        /// cells (count scaling with area, capped at <paramref name="maxPoints"/>, stopping when nothing
        /// is 3 m from the picked set), its exits, and its authored title if any.</summary>
        public static string ZoneList(int maxPoints = 4)
        {
            var map = Zones?.Map;
            if (map == null) return Err("no zone map");
            var r = map.Raster;
            var list = new List<object>();
            var cells = new List<int>();
            foreach (var z in map.Zones)
            {
                cells.Clear();
                for (int gz = 0; gz < r.Height; gz++)
                    for (int gx = 0; gx < r.Width; gx++)
                        if (map.LabelAt(gx, gz) == z.Id - 1) cells.Add(r.Index(gx, gz));
                int want = Mathf.Clamp(1 + (int)(z.Area / 150f), 1, maxPoints);
                var picked = new List<int>();
                int seed = -1; float bd = float.MaxValue;
                foreach (int i in cells)
                {
                    float dx = r.CenterX(i % r.Width) - z.Centroid.X, dz = r.CenterZ(i / r.Width) - z.Centroid.Z, d = dx * dx + dz * dz;
                    if (d < bd) { bd = d; seed = i; }
                }
                if (seed < 0) continue;
                picked.Add(seed);
                while (picked.Count < want)
                {
                    int best = -1; float bestMin = -1f;
                    foreach (int i in cells)
                    {
                        float mn = float.MaxValue;
                        foreach (int s in picked)
                        {
                            float dx = r.CenterX(i % r.Width) - r.CenterX(s % r.Width), dz = r.CenterZ(i / r.Width) - r.CenterZ(s / r.Width);
                            float d = dx * dx + dz * dz;
                            if (d < mn) mn = d;
                        }
                        if (mn > bestMin) { bestMin = mn; best = i; }
                    }
                    if (best < 0 || bestMin < 9f) break;
                    picked.Add(best);
                }
                var pts = new List<object>();
                foreach (int i in picked)
                    pts.Add(new { x = R(r.CenterX(i % r.Width)), y = R(r.Elevation[i]), z = R(r.CenterZ(i / r.Width)) });
                var exits = new List<object>();
                foreach (var e in z.Exits)
                    exits.Add(new { x = R(e.Position.X), y = R(e.Position.Y), z = R(e.Position.Z), to = e.To.Id });
                list.Add(new
                {
                    id = z.Id,
                    cls = z.Class.ToString().ToLowerInvariant(),
                    area = R(z.Area),
                    cx = R(z.Centroid.X), cy = R(z.Centroid.Y), cz = R(z.Centroid.Z),
                    title = Reader?.Descriptions.Current?.ZoneTitle(map, z),
                    points = pts,
                    exits,
                });
            }
            return Ser(new { scene = Zones.Scene, zones = list });
        }

        /// <summary>The registry's things standing in a zone, people and things apart (people are never
        /// described; things are listed with their data key for the object survey).</summary>
        public static string Contents(int zoneId)
        {
            var map = Zones?.Map;
            if (map == null || Reader == null) return Err("no zone map");
            var people = new List<object>();
            var things = new List<object>();
            foreach (IWorldItem it in Reader.Model.Items)
            {
                Snv p;
                string name, key;
                try { p = it.Position; name = it.Name; key = (it as IKeyedWorldItem)?.Key; }
                catch (Exception e) { Reader.Host.LogWarning("DevSurvey.Contents: a proxy threw: " + e.Message); continue; }
                var z = map.ZoneAt(p);
                if (z == null || z.Id != zoneId) continue;
                object row = new { name, key, category = it.Category, accessible = it.IsAccessible, x = R(p.X), y = R(p.Y), z = R(p.Z) };
                if (it.Category == WorldTaxonomy.Npc) people.Add(row); else things.Add(row);
            }
            return Ser(new { people, things });
        }

        /// <summary>Every entity of the active scene, inactive story twins included, with the gate data
        /// the object survey records (the mod's registry sees only the active ones).</summary>
        public static string Entities()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var map = Zones?.Map;
            var list = new List<object>();
            foreach (var e in Resources.FindObjectsOfTypeAll<BasicEntity>())
            {
                if (e == null || e.gameObject.scene != scene) continue;
                Vector3 u = e.transform.position;
                Snv p = WorldConvert.ToSnv(u);
                bool accessible = false;
                try { accessible = e.IsAccessible; } catch (Exception) { }
                var zone = map?.ZoneAt(p);
                list.Add(new
                {
                    name = e.name,
                    type = e.GetIl2CppType().Name,
                    active = e.gameObject.activeInHierarchy,
                    accessible,
                    x = R(p.X), y = R(p.Y), z = R(p.Z),
                    zone = zone?.Id ?? 0,
                });
            }
            return Ser(new { scene = scene.name, entities = list });
        }

        /// <summary>Why each registry thing is or is not offered by the scanner from the character's
        /// spot: the gate flags the scan scope reads.</summary>
        public static string ScanDiag()
        {
            if (Reader == null) return Err("no world reader");
            Snv from = Reader.PlayerPosition;
            var env = Reader.Environment;
            var rows = new List<object>();
            foreach (IWorldItem it in Reader.Model.Items)
            {
                try
                {
                    Snv nearest = it.Bounds.NearestPoint(from);
                    rows.Add(new
                    {
                        name = it.Name, category = it.Category, accessible = it.IsAccessible, visible = it.IsVisible,
                        inView = env.InView(nearest), reach = it.ReachableFrom(from).ToString(),
                        offered = ScanScope.Offered(it, from, env), x = R(it.Position.X), y = R(it.Position.Y), z = R(it.Position.Z),
                    });
                }
                catch (Exception e) { rows.Add(new { name = "(threw) " + e.Message }); }
            }
            return Ser(new { from = Pt(from), items = rows });
        }

        // ---- camera, HUD and fog (stateful) ----

        private static bool _saved;
        private static Vector3 _savedFocus;
        private static float _savedZoom;
        private static bool _savedSlaved;
        private static Camera _uiCamera;
        private static Canvas _cameraSpaceCanvas;
        private static readonly List<KeyValuePair<Sunshine.Unseen.Zone, Sunshine.Unseen.Zone.Status>> _savedUnseen
            = new List<KeyValuePair<Sunshine.Unseen.Zone, Sunshine.Unseen.Zone.Status>>();

        /// <summary>Aim the camera for a capture at a point (mod frame) with a zoom factor (the game's
        /// 0..1 factor, clamped to the scene's limits; the maximum is the widest view), the HUD hidden and
        /// every fog lifted. Saves the previous state on first use.</summary>
        public static string Frame(float x, float y, float z, float zoom = 1f)
        {
            var cam = CameraController.Current;
            if (cam == null) return Err("no camera controller");
            if (!_saved)
            {
                _saved = true;
                _savedFocus = cam.Focus;
                _savedZoom = cam.ZoomFactor;
                _savedSlaved = cam.IsSlaved;
                _savedUnseen.Clear();
                foreach (var zone in UnityEngine.Object.FindObjectsOfType<Sunshine.Unseen.Zone>(true))
                    if (zone.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                        _savedUnseen.Add(new KeyValuePair<Sunshine.Unseen.Zone, Sunshine.Unseen.Zone.Status>(zone, zone.status));
                var ui = GameObject.Find("FinalUICamera");
                _uiCamera = ui != null ? ui.GetComponent<Camera>() : null;
                _cameraSpaceCanvas = null;
                foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                    if (c.isRootCanvas && c.name == "Camera Space Canvas") _cameraSpaceCanvas = c;
            }
            WorldEnvironment.ZoomPinSuspended = true;
            if (_uiCamera != null) _uiCamera.enabled = false;
            if (_cameraSpaceCanvas != null) _cameraSpaceCanvas.enabled = false;
            FOWController.SetVisible(false, true);
            foreach (var kv in _savedUnseen)
                if (kv.Key != null && kv.Key.status == Sunshine.Unseen.Zone.Status.UNSEEN)
                    kv.Key.status = Sunshine.Unseen.Zone.Status.ACTIVE;
            cam.IsSlaved = false;
            Vector3 u = WorldConvert.ToUnity(new Snv(x, y, z));
            cam.SetFocus(u, new Il2CppSystem.Nullable<float>(zoom), true);
            cam.ZoomFactor = zoom;
            return Ser(new { ok = true, focus = Pt(WorldConvert.ToSnv(cam.Focus)), zoomFactor = R(cam.ZoomFactor), limits = new { min = R(cam.GetZoomLimiters().x), max = R(cam.GetZoomLimiters().y) } });
        }

        /// <summary>Whether the painting's page streamer is idle this frame (Amplify Texture: nothing
        /// submitted, nothing pending, nothing uploaded this frame).</summary>
        public static string Settled()
        {
            try
            {
                Assembly asm = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == "AmplifyTexture") asm = a;
                if (asm == null) return Ser(new { settled = true, note = "no AmplifyTexture assembly" });
                var mgrType = asm.GetType("AmplifyTextureManager");
                object mgr = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                object list = mgrType.GetProperty("RuntimeList").GetValue(mgr);
                int count = (int)list.GetType().GetProperty("Count").GetValue(list);
                if (count == 0) return Ser(new { settled = true, note = "no runtime" });
                object atc = list.GetType().GetProperty("Item").GetValue(list, new object[] { 0 });
                object streamer = atc.GetType().GetProperty("PageStreamer").GetValue(atc);
                int submitted = (int)streamer.GetType().GetProperty("SubmittedCount").GetValue(streamer);
                object pendingQ = streamer.GetType().GetProperty("m_pendingQueue").GetValue(streamer);
                int pending = (int)pendingQ.GetType().GetProperty("Count").GetValue(pendingQ);
                int thisFrame = (int)atc.GetType().GetProperty("m_pagesThisFrame").GetValue(atc);
                return Ser(new { settled = submitted == 0 && pending == 0 && thisFrame == 0, submitted, pending, thisFrame });
            }
            catch (Exception e)
            {
                return Ser(new { settled = true, error = e.Message });
            }
        }

        /// <summary>Screen-space labels for the current frame: every registry thing within
        /// <paramref name="radius"/> of the camera focus that projects on screen, as image coordinates
        /// (origin top-left), with its zone, so the authoring pass can name what it sees. Painted detail
        /// is not in the registry; labels only place the entities.</summary>
        public static string Labels(float radius = 30f)
        {
            var cam = Camera.main;
            if (cam == null || Reader == null) return Err("no camera");
            Vector3 centre = CameraController.Current != null ? CameraController.Current.Focus : cam.transform.position;
            var map = Zones?.Map;
            var labels = new List<object>();
            foreach (IWorldItem it in Reader.Model.Items)
            {
                Snv p; string name, key;
                try { p = it.Position; name = it.Name; key = (it as IKeyedWorldItem)?.Key; }
                catch (Exception e) { Reader.Host.LogWarning("DevSurvey.Labels: a proxy threw: " + e.Message); continue; }
                Vector3 u = WorldConvert.ToUnity(p);
                float dx = u.x - centre.x, dz = u.z - centre.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > radius) continue;
                Vector3 sp = cam.WorldToScreenPoint(u);
                if (sp.z <= 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) continue;
                var zone = map?.ZoneAt(p);
                labels.Add(new
                {
                    name, key, category = it.Category, accessible = it.IsAccessible,
                    zone = zone?.Id ?? 0,
                    sx = (int)sp.x, sy = Screen.height - (int)sp.y, d = R(d),
                });
            }
            return Ser(new { w = Screen.width, h = Screen.height, labels });
        }

        /// <summary>The ground under an image point of the current frame (image coordinates, origin
        /// top-left, in an image of <paramref name="imageW"/> by <paramref name="imageH"/> pixels - the
        /// downscaled capture is fine): the camera ray's crossing with the floor at the character's
        /// elevation, snapped to the navmesh when within 2 m, in the mod frame. For placing a detail
        /// seen in a capture.</summary>
        public static string GroundAt(int sx, int sy, int imageW, int imageH)
        {
            var cam = Camera.main;
            if (cam == null) return Err("no camera");
            float px = sx * (Screen.width / (float)imageW), py = Screen.height - sy * (Screen.height / (float)imageH);
            Ray ray = cam.ScreenPointToRay(new Vector3(px, py, 0f));
            var p = Party.Player.Main;
            float floorY = p != null ? p.transform.position.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
            if (!plane.Raycast(ray, out float enter)) return Err("ray misses the floor plane");
            Vector3 hit = ray.GetPoint(enter);
            bool onMesh = UnityEngine.AI.NavMesh.SamplePosition(hit, out UnityEngine.AI.NavMeshHit nav, 2f, -1);
            Snv core = WorldConvert.ToSnv(hit), snapped = onMesh ? WorldConvert.ToSnv(nav.position) : core;
            return Ser(new { x = R(core.X), y = R(core.Y), z = R(core.Z), onMesh, nearest = Pt(snapped), zone = Zones?.ZoneAt(snapped)?.Id ?? 0 });
        }

        /// <summary>A zone's outline in the current frame: its boundary cells (cells with a neighbour in
        /// another zone or off the ground) and its exits, projected to image coordinates (origin top-left),
        /// so the survey can draw where the zone runs on its capture. Off-screen points are dropped.</summary>
        public static string ZoneOutline(int zoneId)
        {
            var map = Zones?.Map;
            var cam = Camera.main;
            if (map == null || cam == null) return Err("no zone map or camera");
            var r = map.Raster;
            int lbl = zoneId - 1;
            var pts = new List<int[]>();
            for (int gz = 0; gz < r.Height; gz++)
                for (int gx = 0; gx < r.Width; gx++)
                {
                    if (map.LabelAt(gx, gz) != lbl) continue;
                    bool edge = gx == 0 || gz == 0 || gx == r.Width - 1 || gz == r.Height - 1
                        || map.LabelAt(gx - 1, gz) != lbl || map.LabelAt(gx + 1, gz) != lbl
                        || map.LabelAt(gx, gz - 1) != lbl || map.LabelAt(gx, gz + 1) != lbl;
                    if (!edge) continue;
                    Vector3 sp = cam.WorldToScreenPoint(WorldConvert.ToUnity(new Snv(r.CenterX(gx), r.Elevation[r.Index(gx, gz)], r.CenterZ(gz))));
                    if (sp.z <= 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) continue;
                    pts.Add(new[] { (int)sp.x, Screen.height - (int)sp.y });
                }
            var exits = new List<int[]>();
            Zone zone = null;
            foreach (var z in map.Zones) if (z.Id == zoneId) zone = z;
            if (zone != null)
                foreach (var e in zone.Exits)
                {
                    Vector3 sp = cam.WorldToScreenPoint(WorldConvert.ToUnity(e.Position));
                    if (sp.z <= 0f || sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) continue;
                    exits.Add(new[] { (int)sp.x, Screen.height - (int)sp.y, e.To.Id });
                }
            return Ser(new { w = Screen.width, h = Screen.height, points = pts, exits });
        }

        /// <summary>Undo the survey session: fog, HUD, zoom pin, camera.</summary>
        public static string Restore()
        {
            WorldEnvironment.ZoomPinSuspended = false;
            if (_uiCamera != null) _uiCamera.enabled = true;
            if (_cameraSpaceCanvas != null) _cameraSpaceCanvas.enabled = true;
            FOWController.SetVisible(true, true);
            if (!_saved) return Ser(new { ok = true, note = "nothing saved" });
            foreach (var kv in _savedUnseen) if (kv.Key != null) kv.Key.status = kv.Value;
            var cam = CameraController.Current;
            if (cam != null)
            {
                cam.SetFocus(_savedFocus, new Il2CppSystem.Nullable<float>(_savedZoom), true);
                cam.IsSlaved = _savedSlaved;
            }
            _saved = false;
            _savedUnseen.Clear();
            return Ser(new { ok = true });
        }

        // ---- helpers ----

        private static float R(float v) => (float)Math.Round(v, 2);
        private static object Pt(Snv p) => new { x = R(p.X), y = R(p.Y), z = R(p.Z) };
        private static string Ser(object o) => JsonSerializer.Serialize(o, Json);
        private static string Err(string message) => Ser(new { error = message });
    }
}
