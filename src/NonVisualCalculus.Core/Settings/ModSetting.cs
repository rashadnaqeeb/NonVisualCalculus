using System;

namespace NonVisualCalculus.Core.Settings
{
    /// <summary>
    /// The shared surface of one mod setting: a stable persistence <see cref="Key"/> (never spoken) and an
    /// authored, spoken <see cref="Label"/>. <see cref="ModSettings"/> holds every setting through this base
    /// in declaration order, so the settings menu can list them as one sequence and pick a cell by concrete
    /// type (<see cref="ToggleSetting"/>, <see cref="RangeSetting"/>).
    /// </summary>
    public abstract class ModSetting
    {
        /// <summary>Stable persistence key (never spoken), e.g. "wall_tone_volume".</summary>
        public string Key { get; }

        private readonly Func<string> _label;

        /// <summary>Authored, spoken label, resolved at read time: the settings live in the host and
        /// outlive both module reloads and a language switch, so a label captured at construction would
        /// speak the startup language forever.</summary>
        public string Label => _label();

        protected ModSetting(string key, Func<string> label)
        {
            Key = key;
            _label = label;
        }
    }
}
