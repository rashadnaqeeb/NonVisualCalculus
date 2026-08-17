using System;

namespace NonVisualCalculus.Core.Settings
{
    /// <summary>
    /// The shared surface of one mod setting: a stable persistence <see cref="Key"/> (never spoken), an
    /// authored, spoken <see cref="Label"/>, and an optional <see cref="Description"/> explaining what the
    /// setting does. <see cref="ModSettings"/> holds every setting through this base in declaration order,
    /// so the settings menu can list them as one sequence and pick a cell by concrete type
    /// (<see cref="ToggleSetting"/>, <see cref="RangeSetting"/>).
    /// </summary>
    public abstract class ModSetting
    {
        /// <summary>Stable persistence key (never spoken), e.g. "wall_tone_volume".</summary>
        public string Key { get; }

        private readonly Func<string> _label;
        private readonly Func<string>? _description;

        /// <summary>Authored, spoken label, resolved at read time: the settings live in the host and
        /// outlive both module reloads and a language switch, so a label captured at construction would
        /// speak the startup language forever.</summary>
        public string Label => _label();

        /// <summary>Authored explanation of what the setting does, spoken after the value when the menu
        /// lands on it (not on the value re-announce after a change), or null for a setting whose label
        /// and value say everything. Resolved at read time, like <see cref="Label"/>.</summary>
        public string? Description => _description?.Invoke();

        protected ModSetting(string key, Func<string> label, Func<string>? description = null)
        {
            Key = key;
            _label = label;
            _description = description;
        }
    }
}
