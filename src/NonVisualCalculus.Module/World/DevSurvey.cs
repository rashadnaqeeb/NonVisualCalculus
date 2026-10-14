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

        /// <summary>Every exit and door the registry knows in the active scene, with its zone: the
        /// survey's route table for visiting the interiors.</summary>
        public static string Exits()
        {
            if (Reader == null) return Err("no world reader");
            var map = Zones?.Map;
            var rows = new List<object>();
            foreach (IWorldItem it in Reader.Model.Items)
            {
                if (it.Category != WorldTaxonomy.Exit && it.Category != WorldTaxonomy.Door) continue;
                Snv p; string name, key;
                try { p = it.Position; name = it.Name; key = (it as IKeyedWorldItem)?.Key; }
                catch (Exception e) { Reader.Host.LogWarning("DevSurvey.Exits: a proxy threw: " + e.Message); continue; }
                rows.Add(new { name, key, category = it.Category, accessible = it.IsAccessible, visible = it.IsVisible,
                               zone = map?.ZoneAt(p)?.Id ?? 0, x = R(p.X), y = R(p.Y), z = R(p.Z) });
            }
            return Ser(new { exits = rows });
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

        /// <summary>The game's location state machines: name, current state, valid states, and the
        /// scene and mod-frame positions of the visual switchers each drives (where its paint changes).</summary>
        public static string States()
        {
            var switchers = new Dictionary<string, List<object>>();
            foreach (var sw in Resources.FindObjectsOfTypeAll<Sunshine.StateSwitcher>())
            {
                if (!sw.gameObject.scene.IsValid() || string.IsNullOrEmpty(sw.locationName)) continue;
                if (!switchers.TryGetValue(sw.locationName, out var list)) { list = new List<object>(); switchers[sw.locationName] = list; }
                Snv p = WorldConvert.ToSnv(sw.transform.position);
                list.Add(new { scene = sw.gameObject.scene.name, name = sw.name, x = R(p.X), y = R(p.Y), z = R(p.Z), presets = sw.activePresetNames });
            }
            var rows = new List<object>();
            var locs = Sunshine.AreaStateConfiguration.locations;
            if (locs != null)
                foreach (var l in locs)
                {
                    if (l == null) continue;
                    var states = l.validStates;
                    string cur = states != null && l.currentState >= 0 && l.currentState < states.Length ? states[l.currentState] : null;
                    switchers.TryGetValue(l.name, out var sws);
                    rows.Add(new { name = l.name, current = cur, states, switchers = sws ?? new List<object>() });
                }
            return Ser(new { locations = rows });
        }

        /// <summary>Switch a location machine to a state through the game's own path (the same call the
        /// story's Lua makes), returning the previous state so the caller can put it back. The visual
        /// switchers apply at once; nothing is saved.</summary>
        public static string SetState(string location, string state)
        {
            var loc = Sunshine.AreaStatePlaceholder.GetLocation(location);
            if (loc == null) return Err("no location '" + location + "'");
            var states = loc.validStates;
            string previous = states != null && loc.currentState >= 0 && loc.currentState < states.Length ? states[loc.currentState] : null;
            if (!loc.HasState(state)) return Err("location '" + location + "' has no state '" + state + "'");
            Sunshine.AreaStatePlaceholder.SwitchTo(location, state);
            states = loc.validStates;
            string now = states != null && loc.currentState >= 0 && loc.currentState < states.Length ? states[loc.currentState] : null;
            return Ser(new { ok = now == state, previous, now });
        }

        // ---- camera, HUD and fog (stateful) ----

        private const float FocusHeight = 1.9f;
        private static bool _saved;
        // The scene the saved state belongs to: a scene change throws it away, since the objects and
        // the character's spot it refers to are gone (a warp to another scene's coordinates would
        // put the character off the navmesh).
        private static string _savedScene;
        private static Vector3 _savedFocus;
        private static float _savedZoom;
        private static Camera _uiCamera;
        private static Canvas _cameraSpaceCanvas;
        // The day-night visual: a capture is lit as noon so the paint reads the same at any game hour.
        private static bool _savedDaytime;
        private static float _savedVisualTime, _savedVisualDesired;
        private static bool _savedVisualAttach, _savedVisualLock;
        private const int NoonTiming = 3; // index into DaytimeVisualController.daytimeTimings
        // The level designers' own work light, left inactive in some scenes ("GRAND LIGHT FOR LEVEL
        // EDITING"): switched on for the captures of an unlit floor and off again by Restore.
        private static readonly List<GameObject> _workLights = new List<GameObject>();
        // The flashlight gate: the dark floors carve their navmesh and hide their rooms until the
        // character wields a flashlight. The survey sets the gate's Lua boolean and fires its
        // switchers, so the character can stand there and the rooms uncover; Restore puts it back.
        private const string FlashlightBoolean = "auto.wielding_flashlight";
        private static bool _savedFlashlight, _savedFlashlightValue;
        // Where the character stood before the first Teleport; Restore warps back there.
        private static bool _savedPlayer;
        private static Vector3 _savedPlayerPos;
        private static readonly List<KeyValuePair<Sunshine.Unseen.Zone, Sunshine.Unseen.Zone.Status>> _savedUnseen
            = new List<KeyValuePair<Sunshine.Unseen.Zone, Sunshine.Unseen.Zone.Status>>();

        /// <summary>Aim the camera for a capture at a point (mod frame) with a zoom factor (the game's
        /// 0..1 factor, clamped to the scene's limits; the maximum is the widest view), the HUD hidden and
        /// every fog lifted. Saves the previous state on first use.</summary>
        public static string Frame(float x, float y, float z, float zoom = 1f)
        {
            var cam = CameraController.Current;
            if (cam == null) return Err("no camera controller");
            ForgetOtherScene();
            if (!_saved)
            {
                _saved = true;
                _savedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                _savedFocus = cam.Focus;
                _savedZoom = cam.ZoomFactor;
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
            ForceNoon();
            LightTheDark();
            if (_uiCamera != null) _uiCamera.enabled = false;
            if (_cameraSpaceCanvas != null) _cameraSpaceCanvas.enabled = false;
            FOWController.SetVisible(false, true);
            foreach (var kv in _savedUnseen)
            {
                // ACTIVE is the status of a room the character stands in (an observer inside); UNSEEN
                // is never entered and INACTIVE is entered and left, both drawn black. Every zone is
                // forced ACTIVE for the capture. A zone the scene keeps disabled throws on the status
                // write; it hides nothing, so skip it.
                try
                {
                    if (kv.Key != null && kv.Key.status != Sunshine.Unseen.Zone.Status.ACTIVE)
                        kv.Key.status = Sunshine.Unseen.Zone.Status.ACTIVE;
                }
                catch (Exception e) { Reader?.Host.LogWarning("DevSurvey.Frame: fog zone " + kv.Key?.name + ": " + e.Message); }
            }
            cam.IsSlaved = false;
            // The game's slaved focus sits about two metres above the character's feet; a focus at
            // floor height puts the near-horizontal camera into the floor and the frame goes black.
            Vector3 u = WorldConvert.ToUnity(new Snv(x, y + FocusHeight, z));
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

        // Objects switched on for an inactive-twin capture, innermost last, so RestoreEntities puts
        // them back in reverse. Never saved: the game must not be saved during a survey.
        private static readonly List<GameObject> _activated = new List<GameObject>();
        // An absent person's DeactivatableObject switches them straight back off in OnEnable unless
        // its three visibility flags all hold; the flags are raised for the capture and put back after.
        private static readonly List<(Sunshine.DeactivatableObject d, bool area, bool sched, bool dev)> _shown
            = new List<(Sunshine.DeactivatableObject, bool, bool, bool)>();

        /// <summary>Switch on an inactive entity (and the inactive ancestors that hide it) so the
        /// object survey can capture the early-game twin of a looted container or story prop; the
        /// first match by name wins. <see cref="RestoreEntities"/> switches them back off.</summary>
        /// <summary>The live position (mod frame) of the first entity with this name: people move on
        /// schedules, so a capture of a person frames on where they stand now, not where the dump saw them.</summary>
        public static string EntityPosition(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var e in Resources.FindObjectsOfTypeAll<BasicEntity>())
            {
                if (e == null || e.gameObject.scene != scene || e.name != name) continue;
                Snv p = WorldConvert.ToSnv(e.transform.position);
                return Ser(new { ok = true, x = R(p.X), y = R(p.Y), z = R(p.Z), active = e.gameObject.activeInHierarchy });
            }
            return Ser(new { ok = false, error = "no entity named " + name });
        }

        public static string ActivateEntity(string name, bool deep = false)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var e in Resources.FindObjectsOfTypeAll<BasicEntity>())
            {
                if (e == null || e.gameObject.scene != scene || e.name != name) continue;
                var chain = new List<GameObject>();
                if (!e.gameObject.activeInHierarchy)
                {
                    for (Transform tr = e.transform; tr != null; tr = tr.parent)
                        if (!tr.gameObject.activeSelf) chain.Add(tr.gameObject);
                    chain.Reverse();
                }
                // A person's body is a child the schedule switches on when they are placed; for a capture
                // of an absent person the body is switched on with them (and off again with Restore).
                if (deep)
                    foreach (var tr in e.GetComponentsInChildren<Transform>(true))
                        if (tr != e.transform && !tr.gameObject.activeSelf) chain.Add(tr.gameObject);
                foreach (var go in chain)
                {
                    var d = go.GetComponent<Sunshine.DeactivatableObject>();
                    if (d != null)
                    {
                        _shown.Add((d, d.areastateVisible, d.schedulerVisible, d.developerVisible));
                        d.SetAreaStateVisible(true); d.SetSchedulerVisible(true); d.SetDeveloperVisible(true);
                    }
                    go.SetActive(true); _activated.Add(go);
                }
                return Ser(new { ok = true, switched = chain.Count, active = e.gameObject.activeInHierarchy });
            }
            return Ser(new { ok = false, error = "no entity named " + name });
        }

        /// <summary>Switch off everything <see cref="ActivateEntity"/> switched on.</summary>
        public static string RestoreEntities()
        {
            int n = 0;
            for (int i = _activated.Count - 1; i >= 0; i--)
            {
                try { if (_activated[i] != null) { _activated[i].SetActive(false); n++; } }
                catch (Exception e) { Reader?.Host.LogWarning("DevSurvey.RestoreEntities: " + e.Message); }
            }
            _activated.Clear();
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var s = _shown[i];
                try { if (s.d != null) { s.d.SetAreaStateVisible(s.area); s.d.SetSchedulerVisible(s.sched); s.d.SetDeveloperVisible(s.dev); } }
                catch (Exception e) { Reader?.Host.LogWarning("DevSurvey.RestoreEntities: visibility flags: " + e.Message); }
            }
            _shown.Clear();
            return Ser(new { ok = true, restored = n });
        }

        /// <summary>Light the scene as noon for the captures: the visual controller follows the game
        /// clock and locks its refresh, so both are suspended and the noon timing applied at once.</summary>
        private static void ForceNoon()
        {
            var v = DaytimeVisualController.Singleton;
            if (v == null) return;
            if (!_savedDaytime)
            {
                _savedDaytime = true;
                _savedVisualTime = v.time;
                _savedVisualDesired = v.desiredTime;
                _savedVisualAttach = v.attachToSunshineClock;
                _savedVisualLock = DaytimeVisualController.lockVisuals;
            }
            v.attachToSunshineClock = false;
            DaytimeVisualController.lockVisuals = false;
            DaytimeVisualController.SetDesiredTime(NoonTiming, true);
        }

        /// <summary>Drop every saved value when the active scene is not the one it was saved in.</summary>
        private static void ForgetOtherScene()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (_savedScene == null || _savedScene == scene) return;
            Reader?.Host.LogWarning("DevSurvey: scene changed from " + _savedScene + " to " + scene + " with survey state saved; dropping it unrestored");
            _saved = false; _savedUnseen.Clear(); _savedDaytime = false; _savedPlayer = false; _savedFlashlight = false;
            _workLights.Clear(); _activated.Clear(); _savedScene = null;
        }

        private static void LightTheDark()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (_workLights.Count == 0)
                foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
                    if (l.gameObject.scene == scene && !l.gameObject.activeSelf && l.name.StartsWith("GRAND LIGHT"))
                    {
                        l.gameObject.SetActive(true);
                        _workLights.Add(l.gameObject);
                    }
            if (!_savedFlashlight)
            {
                _savedFlashlight = true;
                _savedFlashlightValue = PixelCrushers.DialogueSystem.DialogueLua.GetVariable(FlashlightBoolean).asBool;
                if (!_savedFlashlightValue) SetFlashlight(true);
            }
        }

        private static void SetFlashlight(bool on)
        {
            PixelCrushers.DialogueSystem.DialogueLua.SetVariable(FlashlightBoolean, on);
            foreach (var sw in UnityEngine.Object.FindObjectsOfType<LuaBlockerSwitcher>(true))
                if (sw.booleanName == FlashlightBoolean) sw.BooleanChanged(FlashlightBoolean);
            foreach (var sw in UnityEngine.Object.FindObjectsOfType<LuaGameObjectsSwitcher>(true))
                if (sw.booleanName == FlashlightBoolean) sw.BooleanChanged(FlashlightBoolean);
            // The zone map is left alone: a rebuild would flood from wherever the character has been
            // warped to, and the survey's zone ids must stay those of the map the scene built on entry.
        }

        /// <summary>Warp the character to a point (mod frame) on the navmesh, so the game cuts away
        /// the roofs and lifts the room fog around it the way it does in play; the camera is not
        /// moved. Saves the previous spot on first use; <see cref="Restore"/> warps back.</summary>
        public static string Teleport(float x, float y, float z)
        {
            var player = Party.Player.Main;
            if (player == null) return Err("no player");
            ForgetOtherScene();
            if (_savedScene == null) _savedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Vector3 u = WorldConvert.ToUnity(new Snv(x, y, z));
            if (!UnityEngine.AI.NavMesh.SamplePosition(u, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                return Err("no navmesh within 4 m of the point");
            if (!_savedPlayer) { _savedPlayer = true; _savedPlayerPos = player.transform.position; }
            if (agent != null) agent.Warp(hit.position); else player.transform.position = hit.position;
            return Ser(new { ok = true, at = Pt(WorldConvert.ToSnv(hit.position)), moved = R(Vector3.Distance(u, hit.position)) });
        }

        /// <summary>Undo the survey session: fog, HUD, zoom pin, camera, lighting, the character's spot.</summary>
        public static string Restore()
        {
            // Each step restores on its own: a throw in one (a fog zone whose object the scene has
            // since destroyed) must not leave the camera or the HUD in the survey's state.
            var problems = new List<string>();
            ForgetOtherScene();
            WorldEnvironment.ZoomPinSuspended = false;
            try { if (_uiCamera != null) _uiCamera.enabled = true; } catch (Exception e) { problems.Add("ui camera: " + e.Message); }
            try { if (_cameraSpaceCanvas != null) _cameraSpaceCanvas.enabled = true; } catch (Exception e) { problems.Add("canvas: " + e.Message); }
            try { FOWController.SetVisible(true, true); } catch (Exception e) { problems.Add("fog: " + e.Message); }
            if (_savedDaytime)
            {
                try
                {
                    var v = DaytimeVisualController.Singleton;
                    if (v != null)
                    {
                        v.time = _savedVisualTime;
                        v.desiredTime = _savedVisualDesired;
                        DaytimeVisualController.RefreshVisual();
                        v.attachToSunshineClock = _savedVisualAttach;
                        DaytimeVisualController.lockVisuals = _savedVisualLock;
                    }
                }
                catch (Exception e) { problems.Add("daytime: " + e.Message); }
                _savedDaytime = false;
            }
            foreach (var go in _workLights)
            {
                try { if (go != null) go.SetActive(false); } catch (Exception e) { problems.Add("work light: " + e.Message); }
            }
            _workLights.Clear();
            if (_savedFlashlight)
            {
                try { if (!_savedFlashlightValue) SetFlashlight(false); } catch (Exception e) { problems.Add("flashlight: " + e.Message); }
                _savedFlashlight = false;
            }
            if (_savedPlayer)
            {
                try
                {
                    var player = Party.Player.Main;
                    var agent = player?.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null) agent.Warp(_savedPlayerPos); else if (player != null) player.transform.position = _savedPlayerPos;
                }
                catch (Exception e) { problems.Add("player: " + e.Message); }
                _savedPlayer = false;
            }
            if (!_saved) return Ser(new { ok = problems.Count == 0, note = "nothing saved", problems });
            var cam = CameraController.Current;
            if (cam != null)
            {
                try
                {
                    cam.SetFocus(_savedFocus, new Il2CppSystem.Nullable<float>(_savedZoom), true);
                    // Always back to the game's play state: a survey never runs inside a cutscene, and a
                    // saved "unslaved" would only ever be an earlier survey's leftover.
                    cam.IsSlaved = true;
                }
                catch (Exception e) { problems.Add("camera: " + e.Message); }
            }
            foreach (var kv in _savedUnseen)
            {
                try { if (kv.Key != null) kv.Key.status = kv.Value; }
                catch (Exception e) { problems.Add("unseen zone: " + e.Message); }
            }
            _saved = false;
            _savedUnseen.Clear();
            _savedScene = null;
            foreach (string pr in problems) Reader?.Host.LogWarning("DevSurvey.Restore: " + pr);
            return Ser(new { ok = problems.Count == 0, problems });
        }

        // ---- helpers ----

        private static float R(float v) => (float)Math.Round(v, 2);
        private static object Pt(Snv p) => new { x = R(p.X), y = R(p.Y), z = R(p.Z) };
        private static string Ser(object o) => JsonSerializer.Serialize(o, Json);
        private static string Err(string message) => Ser(new { error = message });
    }
}
