using System.Collections.Generic;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.Settings
{
    /// <summary>
    /// The mod's settings: each one declared once here, loaded from the store on construction and persisted
    /// as it changes. The host owns the single instance (built with its concrete <see cref="ISettingsStore"/>)
    /// and lends it to the module through <c>IModHost.Settings</c>, the same way it lends the speech pipeline,
    /// so the values survive a module hot-reload. Feature code reads a setting by its strongly-typed property
    /// (<see cref="AutoReadDialogue"/>); the settings menu iterates <see cref="Toggles"/>.
    /// </summary>
    public sealed class ModSettings
    {
        private readonly List<ModSetting> _all = new List<ModSetting>();

        /// <summary>Every setting, in declaration order, for the settings menu to list.</summary>
        public IReadOnlyList<ModSetting> All => _all;

        /// <summary>Speak each new conversation line automatically as it is delivered. Off lands the cursor
        /// on the line silently, leaving the player to read it on their own terms.</summary>
        public ToggleSetting AutoReadDialogue { get; }

        /// <summary>Speak the world's background barks (TV, NPC chatter, proximity remarks) as they float up,
        /// queued so they never cut off the player. Off leaves the world silent of ambient talk.</summary>
        public ToggleSetting ReadAmbientDialogue { get; }

        /// <summary>The loudness of the directional wall tones, a 0..100 percent. Defaults low (5%): the tones
        /// are an ambient orientation bed, not a foreground cue, so they sit under speech until turned up.</summary>
        public RangeSetting WallToneVolume { get; }

        /// <summary>When on, wall tones sound continuously while in the world; when off (the default) they
        /// sound only while the cursor is gliding, lingering briefly after it stops.</summary>
        public ToggleSetting WallTonesContinuous { get; }

        public ModSettings(ISettingsStore store)
        {
            AutoReadDialogue = Add(new ToggleSetting(
                "auto_read_dialogue", SettingAutoReadDialogue, defaultValue: true, store));
            ReadAmbientDialogue = Add(new ToggleSetting(
                "read_ambient_dialogue", SettingReadAmbientDialogue, defaultValue: true, store));
            WallToneVolume = Add(new RangeSetting(
                "wall_tone_volume", SettingWallToneVolume, defaultValue: 5, step: 5, store));
            WallTonesContinuous = Add(new ToggleSetting(
                "wall_tones_continuous", SettingWallTonesContinuous, defaultValue: false, store));
        }

        private T Add<T>(T setting) where T : ModSetting
        {
            _all.Add(setting);
            return setting;
        }
    }
}
