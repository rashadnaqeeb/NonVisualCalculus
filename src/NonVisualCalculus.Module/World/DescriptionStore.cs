using System;
using System.Collections.Generic;
using System.IO;
using I2.Loc;
using NonVisualCalculus.Core.Descriptions;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The authored descriptions of the active scene, loaded from the plugin's descriptions folder:
    /// <c>&lt;scene&gt;.txt</c> (geometry and keys), <c>&lt;scene&gt;.en.txt</c> (the English prose, the
    /// per-key fallback) and <c>&lt;scene&gt;.&lt;lang&gt;.txt</c> for the game's language, found by the
    /// same language-code-or-name rule as the lang files. Reloaded when the scene or the game language
    /// changes; a module reload makes a fresh store. This is mod data, not game state, so holding it is
    /// fine. On load the scene's curation goes to the <see cref="ZoneTracker"/> (which rebuilds) and the
    /// details become scan items in the <see cref="WorldModel"/>. Story-state variants resolve through
    /// the game's location-state machines, read live at speak time.
    /// </summary>
    public sealed class DescriptionStore
    {
        private readonly IModHost _host;
        private readonly ZoneTracker _zones;
        private readonly WorldModel _model;
        private readonly WorldEnvironment _env;
        private string _loadedScene;
        private string _loadedLanguage;

        /// <summary>The active scene's descriptions, or null when it has none.</summary>
        public SceneDescriptions Current { get; private set; }

        internal DescriptionStore(IModHost host, ZoneTracker zones, WorldModel model, WorldEnvironment env)
        {
            _host = host;
            _zones = zones;
            _model = model;
            _env = env;
        }

        /// <summary>Keep the set current for the active scene and the game's language.</summary>
        public void Tick(string scene)
        {
            string language;
            try { language = LocalizationManager.CurrentLanguage ?? ""; }
            catch (Exception e) { _host.LogWarning("DescriptionStore: reading the game language failed: " + e.Message); language = ""; }
            if (scene == _loadedScene && language == _loadedLanguage) return;
            _loadedScene = scene;
            _loadedLanguage = language;
            Load(scene, language);
        }

        /// <summary>Drop and reload the active scene's files (a dev edit on disk).</summary>
        public void Reload()
        {
            _loadedScene = null;
        }

        private void Load(string scene, string language)
        {
            Current = null;
            try
            {
                string dir = Path.Combine(Path.GetDirectoryName(typeof(Translation).Assembly.Location) ?? "", "descriptions");
                string geomPath = Path.Combine(dir, scene + ".txt");
                if (File.Exists(geomPath))
                {
                    var geometry = SceneGeometry.Parse(File.ReadAllText(geomPath), out List<string> errors);
                    foreach (string e in errors) _host.LogWarning($"descriptions {scene}.txt: {e}");
                    SceneProse english = LoadProse(Path.Combine(dir, scene + ".en.txt"));
                    SceneProse prose = english;
                    foreach (string stem in LanguageSync.FileStems(language))
                    {
                        if (stem == "en") break;
                        string path = Path.Combine(dir, scene + "." + stem + ".txt");
                        if (File.Exists(path)) { prose = LoadProse(path); break; }
                    }
                    Current = new SceneDescriptions(scene, geometry, prose, english, StateOf);
                    _host.LogInfo($"descriptions {scene}: {geometry.Zones.Count} zones, {geometry.Details.Count} details, "
                                  + $"{geometry.Objects.Count} objects, {geometry.Curation.Walls.Count} walls, {geometry.Curation.Merges.Count} merges");
                }
            }
            catch (Exception e)
            {
                _host.LogError($"DescriptionStore: loading {scene} failed: {e}");
                Current = null;
            }
            _zones.Curation = Current?.Geometry.Curation ?? Core.World.Zoning.ZoneCuration.None;
            _zones.Rebuild();
            var details = new List<DetailProxy>();
            if (Current != null)
                foreach (var d in Current.Geometry.Details)
                    details.Add(new DetailProxy(d, () => Current, _env, _host.Speech));
            _model.SetDetails(details);
        }

        private SceneProse LoadProse(string path)
        {
            if (!File.Exists(path)) return SceneProse.Empty;
            var prose = SceneProse.Parse(File.ReadAllText(path), out List<string> errors);
            foreach (string e in errors) _host.LogWarning($"descriptions {Path.GetFileName(path)}: {e}");
            return prose;
        }

        // A location-state machine's live state by name (Sunshine.AreaStateConfiguration.locations), for
        // the prose variants; null when no such location exists.
        private string StateOf(string location)
        {
            try
            {
                var locs = Sunshine.AreaStateConfiguration.locations;
                if (locs == null) return null;
                foreach (var l in locs)
                {
                    if (l == null || !string.Equals(l.name, location, StringComparison.OrdinalIgnoreCase)) continue;
                    var states = l.validStates;
                    return states != null && l.currentState >= 0 && l.currentState < states.Length ? states[l.currentState] : null;
                }
            }
            catch (Exception e)
            {
                _host.LogWarning("DescriptionStore: reading location state '" + location + "' failed: " + e.Message);
            }
            return null;
        }
    }
}
