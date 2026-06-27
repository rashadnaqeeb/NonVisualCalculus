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

        // Options screen: control-type words, spoken after the setting name so the user knows the
        // interaction model (adjust, toggle, or open a menu).
        public const string ControlSlider = "slider";
        public const string ControlToggle = "toggle";
        public const string ControlDropdown = "dropdown";

        // Toggle status words.
        public const string StatusOn = "on";
        public const string StatusOff = "off";

        // Stepped-slider step words. Menu Size and Dialogue Text Size are a size scale; the game keeps
        // no per-step label on the slider, so these are authored.
        public const string StepSmall = "small";
        public const string StepMedium = "medium";
        public const string StepLarge = "large";

        // Spoken after the game's localized tab name when the options tab changes.
        public const string Tab = "tab";

        // The language the player taps Q/L to switch to in play (the game's internal "switchable"
        // language). It is drawn under the shared LANGUAGE header with no label of its own, so authored.
        public const string SecondaryLanguage = "secondary language";

        /// <summary>A continuous slider's position as a percentage of its travel.</summary>
        public static string Percent(int value) => value + " percent";

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => "step " + index + " of " + count;
    }
}
