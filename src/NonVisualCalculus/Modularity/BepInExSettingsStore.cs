using System.Collections.Generic;
using BepInEx.Configuration;
using NonVisualCalculus.Core.Settings;

namespace NonVisualCalculus.Modularity
{
    /// <summary>
    /// Persists the mod's settings through BepInEx's <see cref="ConfigFile"/> (the host plugin's own
    /// <c>Config</c>), so each setting lands in a single TOML file under <c>BepInEx/config</c> that survives
    /// game restarts and is editable by hand. Each key binds a <see cref="ConfigEntry{T}"/> once and is then
    /// reused; setting its value auto-saves the file.
    /// </summary>
    internal sealed class BepInExSettingsStore : ISettingsStore
    {
        private const string Section = "Settings";
        private readonly ConfigFile _config;
        private readonly Dictionary<string, ConfigEntry<bool>> _bools = new Dictionary<string, ConfigEntry<bool>>();
        private readonly Dictionary<string, ConfigEntry<int>> _ints = new Dictionary<string, ConfigEntry<int>>();

        public BepInExSettingsStore(ConfigFile config) => _config = config;

        private ConfigEntry<bool> Bind(string key, bool defaultValue)
        {
            if (!_bools.TryGetValue(key, out ConfigEntry<bool> entry))
            {
                entry = _config.Bind(Section, key, defaultValue);
                _bools[key] = entry;
            }
            return entry;
        }

        private ConfigEntry<int> BindInt(string key, int defaultValue)
        {
            if (!_ints.TryGetValue(key, out ConfigEntry<int> entry))
            {
                entry = _config.Bind(Section, key, defaultValue);
                _ints[key] = entry;
            }
            return entry;
        }

        public bool GetBool(string key, bool defaultValue) => Bind(key, defaultValue).Value;

        public void SetBool(string key, bool value) => Bind(key, value).Value = value;

        public int GetInt(string key, int defaultValue) => BindInt(key, defaultValue).Value;

        public void SetInt(string key, int value) => BindInt(key, value).Value = value;
    }
}
