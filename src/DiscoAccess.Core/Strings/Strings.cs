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

        // Input action descriptions: the human-readable name of each bound action, for a keybindings
        // reader to speak. DE names none of these (they are our own controls), so they are authored.
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
        public const string RoleTab = "tab";

        // Marks the tab whose content is currently shown (the options screen's Settings/Controls tabs).
        public const string StatusSelected = "selected";

        // Marks the save menu's create-new slot, so activating it reads as a new save, not an overwrite.
        public const string StatusNewSave = "new save";

        // The save menu's rename column. The action has no game button to read a caption from (editing is
        // triggered by clicking the entry), so its label is authored; activating it puts the save-name
        // field into edit mode, announced with StatusEditMode, after which regular typing fills it.
        public const string ActionRename = "rename";
        public const string StatusEditMode = "edit mode";

        // Readable key names for the options Controls tab, which draws each key as an icon image with no
        // text. Only keys whose icon name is not already readable need an entry; single letters and
        // function keys (C, F1) are read straight from the sprite name. See the module's KeyGlyph.
        public const string KeyEscape = "Escape";
        public const string KeyTab = "Tab";
        public const string KeyLeftClick = "left click";
        public const string KeyRightClick = "right click";
        public const string KeyMouseWheel = "mouse wheel";

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
        public const string ScreenPauseMenu = "pause menu";
        public const string ScreenHelp = "help";
        public const string ScreenThought = "thought";
        public const string ScreenCollage = "collage mode";

        // The language the player taps Q/L to switch to in play (the game's internal "switchable"
        // language). It is drawn under the shared LANGUAGE header with no label of its own, so authored.
        public const string SecondaryLanguage = "secondary language";

        // Type-ahead search: spoken when clearing a live search (Escape), and the buffer text first
        // (the distinguishing part) when it matches nothing. DE has no equivalent strings, so authored.
        public const string SearchCleared = "search cleared";

        /// <summary>Spoken when the typed search buffer matches no item in the focused list.</summary>
        public static string SearchNoMatch(string buffer) => buffer + ", no match";

        /// <summary>A continuous slider's position as a percentage of its travel.</summary>
        public static string Percent(int value) => value + " percent";

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => "step " + index + " of " + count;
    }
}
