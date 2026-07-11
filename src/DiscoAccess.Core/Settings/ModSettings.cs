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

        /// <summary>The loudness of the directional wall tones, a 0..100 percent, defaulting to 50: an
        /// orientation bed loud enough to hear without competing with speech.</summary>
        public RangeSetting WallToneVolume { get; }

        /// <summary>When on, wall tones sound continuously while in the world; when off (the default) they
        /// sound only while the cursor is gliding, lingering briefly after it stops.</summary>
        public ToggleSetting WallTonesContinuous { get; }

        /// <summary>The loudness of the sonar sweep's pings and the scanner's review ping - one knob, since
        /// they are the same sounds through the same falloff. A 0..100 percent, defaulting to the WOTR
        /// review-cue level.</summary>
        public RangeSetting SonarVolume { get; }

        /// <summary>When on, the sonar sweeps continuously while in the world; when off (the default) it
        /// sweeps only while the cursor is gliding, lingering briefly after it stops.</summary>
        public ToggleSetting SonarContinuous { get; }

        /// <summary>The rest between sonar sweeps, in milliseconds (within a sweep the pings pace
        /// themselves by count). WOTR's default and span.</summary>
        public RangeSetting SonarRest { get; }

        // What the sonar sonifies, one toggle per browse category (all on by default); turning every one
        // off silences the sonar. The scanner browses everything regardless.
        public ToggleSetting SonarNpcs { get; }
        public ToggleSetting SonarInteractables { get; }
        public ToggleSetting SonarContainers { get; }
        public ToggleSetting SonarOrbs { get; }
        public ToggleSetting SonarExits { get; }

        /// <summary>When on (the default), the scanner's readout - the spoken bearing and distance, the
        /// review ping's ear, and the browse order - measures from the cursor instead of the character (the
        /// sonar's own listening-ear rule). What is offered stays judged from the character, whose walk
        /// acting on a scanned thing starts. Off measures everything from the character.</summary>
        public ToggleSetting ScannerFromCursor { get; }

        /// <summary>When on, the character runs to a clicked destination instead of walking. Off (the default)
        /// leaves the pace to the game's own policy, which walks, matching a vanilla single click.</summary>
        public ToggleSetting RunToDestinations { get; }

        /// <summary>When on (the default), the cursor glides past the senses' edges - out of the visible
        /// frame and into fog-of-war ground - instead of being refused there, with the fog enter/exit cues
        /// sounding the crossings. The scanner and object senses are unchanged.</summary>
        public ToggleSetting UnrestrictCursor { get; }

        public ModSettings(ISettingsStore store)
        {
            // Labels are providers, not captured strings: the settings outlive module reloads and a
            // language switch, so each label resolves through the strings table at speak time.
            AutoReadDialogue = Add(new ToggleSetting(
                "auto_read_dialogue", () => SettingAutoReadDialogue, defaultValue: true, store));
            ReadAmbientDialogue = Add(new ToggleSetting(
                "read_ambient_dialogue", () => SettingReadAmbientDialogue, defaultValue: true, store));
            WallToneVolume = Add(new RangeSetting(
                "wall_tone_volume", () => SettingWallToneVolume, defaultValue: 50, step: 5, store));
            WallTonesContinuous = Add(new ToggleSetting(
                "wall_tones_continuous", () => SettingWallTonesContinuous, defaultValue: false, store));
            SonarVolume = Add(new RangeSetting(
                "sonar_volume", () => SettingSonarVolume, defaultValue: 70, step: 5, store));
            SonarContinuous = Add(new ToggleSetting(
                "sonar_continuous", () => SettingSonarContinuous, defaultValue: false, store));
            SonarRest = Add(new RangeSetting(
                "sonar_rest", () => SettingSonarRest, defaultValue: 400, step: 50, min: 0, max: 1500,
                RangeUnit.Milliseconds, store));
            SonarNpcs = Add(new ToggleSetting(
                "sonar_npc", () => SettingSonarNpcs, defaultValue: true, store));
            SonarInteractables = Add(new ToggleSetting(
                "sonar_interactable", () => SettingSonarInteractables, defaultValue: true, store));
            SonarContainers = Add(new ToggleSetting(
                "sonar_container", () => SettingSonarContainers, defaultValue: true, store));
            SonarOrbs = Add(new ToggleSetting(
                "sonar_orb", () => SettingSonarOrbs, defaultValue: true, store));
            SonarExits = Add(new ToggleSetting(
                "sonar_exit", () => SettingSonarExits, defaultValue: true, store));
            ScannerFromCursor = Add(new ToggleSetting(
                "scanner_from_cursor", () => SettingScannerFromCursor, defaultValue: true, store));
            RunToDestinations = Add(new ToggleSetting(
                "run_to_destinations", () => SettingRunToDestinations, defaultValue: false, store));
            UnrestrictCursor = Add(new ToggleSetting(
                "unrestrict_cursor", () => SettingUnrestrictCursor, defaultValue: true, store));
        }

        /// <summary>Whether the sonar should sound the given <see cref="World.WorldTaxonomy.Scan"/> browse
        /// category key - the seam the sonar's category filter binds to.</summary>
        public bool SonarCategoryEnabled(string scanCategory)
        {
            switch (scanCategory)
            {
                case World.WorldTaxonomy.Npc: return SonarNpcs.Value;
                case World.WorldTaxonomy.Interactable: return SonarInteractables.Value;
                case World.WorldTaxonomy.Container: return SonarContainers.Value;
                case World.WorldTaxonomy.Orb: return SonarOrbs.Value;
                case World.WorldTaxonomy.Exit: return SonarExits.Value;
                default: return true; // an unmapped category is never silently dropped
            }
        }

        private T Add<T>(T setting) where T : ModSetting
        {
            _all.Add(setting);
            return setting;
        }
    }
}
