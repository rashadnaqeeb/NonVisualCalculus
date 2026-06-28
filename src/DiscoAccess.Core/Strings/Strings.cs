namespace DiscoAccess.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read
    /// live and already localized). Keeping authored strings here, not as inline literals, so the
    /// set can be translated later. Game-content reading must never route through here.
    /// </summary>
    public static class Strings
    {
        public const string ModLoaded = "Disco Elysium access loaded";
        public const string ModuleFailed = "DiscoAccess features failed to load";

        // Focus mode: our keyboard-navigation master switch. On engages our own UI navigation (and mutes
        // the game's menu input); off hands the keyboard back to the game. Spoken on toggle.
        public const string FocusModeOn = "focus mode on";
        public const string FocusModeOff = "focus mode off";

        // Input action descriptions: the human-readable name of each bound action, for a keybindings
        // reader to speak. DE names none of these (they are our own controls), so they are authored.
        public const string InputToggleFocusMode = "Toggle focus mode";
        public const string InputNavigateUp = "Navigate up";
        public const string InputNavigateDown = "Navigate down";
        public const string InputNavigateLeft = "Navigate left";
        public const string InputNavigateRight = "Navigate right";
        public const string InputNextControl = "Next control";
        public const string InputPrevControl = "Previous control";
        public const string InputActivate = "Activate control";
        public const string InputBack = "Back";
        public const string InputJumpFirst = "Jump to first";
        public const string InputJumpLast = "Jump to last";

        // Control role words, spoken after a control's label so the user knows what it is.
        public const string RoleButton = "button";
        public const string RoleList = "list";

        // Options screen: control-type words, spoken after the setting name so the user knows the
        // interaction model (adjust, toggle, or open a menu).
        public const string ControlSlider = "slider";
        public const string ControlToggle = "toggle";
        public const string ControlDropdown = "dropdown";

        // Toggle status words.
        public const string StatusOn = "on";
        public const string StatusOff = "off";

        // Marks the skill currently chosen as the signature skill, on the signature skill screen. DE
        // shows this only as an emblem on the portrait, with no localized word to read, so it is authored.
        public const string StatusSignature = "signature";

        // Stepped-slider step words. Menu Size and Dialogue Text Size are a size scale; the game keeps
        // no per-step label on the slider, so these are authored.
        public const string StepSmall = "small";
        public const string StepMedium = "medium";
        public const string StepLarge = "large";

        // Screen names, spoken when a screen opens (the landed control then queues behind). DE exposes
        // its screens only as a Unity enum with no localized title, so these are authored; mapped from
        // the live enum in ScreenAdapter, which names every player-facing view.
        public const string ScreenWorld = "world";
        public const string ScreenInventory = "inventory";
        public const string ScreenClothing = "clothing";
        public const string ScreenThoughtCabinet = "thought cabinet";
        public const string ScreenJournal = "journal";
        public const string ScreenCharacterSheet = "character sheet";
        public const string ScreenArchetypeSelection = "archetype selection";
        public const string ScreenAdjustAbilities = "adjust abilities";
        public const string ScreenSignatureSkill = "signature skill";
        public const string ScreenOptions = "options";
        public const string ScreenSave = "save game";
        public const string ScreenLoad = "load game";
        public const string ScreenMainMenu = "main menu";
        public const string ScreenHelp = "help";
        public const string ScreenThought = "thought";
        public const string ScreenCollage = "collage mode";

        // The language the player taps Q/L to switch to in play (the game's internal "switchable"
        // language). It is drawn under the shared LANGUAGE header with no label of its own, so authored.
        public const string SecondaryLanguage = "secondary language";

        /// <summary>A continuous slider's position as a percentage of its travel.</summary>
        public static string Percent(int value) => value + " percent";

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => "step " + index + " of " + count;
    }
}
