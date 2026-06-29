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
        public const string InputSecondary = "Secondary action";

        // Control role words, spoken after a control's label so the user knows what it is.
        public const string RoleButton = "button";
        public const string RoleList = "list";
        public const string RoleTab = "tab";

        // Marks the tab whose content is currently shown (the journal screen's tabs and filter toggles).
        public const string StatusSelected = "selected";

        // Marks the save menu's create-new slot, so activating it reads as a new save, not an overwrite.
        public const string StatusNewSave = "new save";

        // The save menu's rename column. The action has no game button to read a caption from (editing is
        // triggered by clicking the entry), so its label is authored; activating it puts the save-name
        // field into edit mode, announced with StatusEditMode, after which regular typing fills it.
        public const string ActionRename = "rename";
        public const string StatusEditMode = "edit mode";

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

        // Marks a skill on the character sheet that a skill point can be spent on right now (the skill is
        // upgradeable and points remain). DE shows this only as a glow on the portrait, no word to read.
        public const string StatusCanRaise = "can raise";

        // The character sheet's pool of unspent skill points, spoken as "{n} skill points". DE shows it
        // only as a row of pips with no spoken label.
        public const string StatusSkillPoints = "skill points";

        // Spoken on the Adjust Abilities screen when a raise is rejected because the shared ability-point
        // pool is empty. DE only greys out the plus button, with no message a blind player could hear.
        public const string AbilityNoPointsLeft = "no more points to assign";

        // Stepped-slider step words. Menu Size and Dialogue Text Size are a size scale; the game keeps
        // no per-step label on the slider, so these are authored.
        public const string StepSmall = "small";
        public const string StepMedium = "medium";
        public const string StepLarge = "large";

        // Spoken when a slider or stepper is adjusted past its end, so the user hears that it stopped
        // rather than the same value read back twice.
        public const string StatusMinimum = "minimum";
        public const string StatusMaximum = "maximum";

        // Thought cabinet status words. The slot and thought states are game enums shown only as art (slot
        // frames, text colors), with no spoken label, so these are authored. A slot is empty (unlocked,
        // unfilled), unlockable (can be bought now), or locked (not yet buyable); a thought is available
        // (gathered, not placed), researching (placed and cooking), researched (fixed), forgotten, or
        // undiscovered.
        public const string ThoughtSlotEmpty = "empty slot";
        public const string ThoughtSlotUnlockable = "locked slot, can unlock";
        public const string ThoughtSlotLocked = "locked slot";
        public const string ThoughtAvailable = "available";
        public const string ThoughtResearched = "researched";
        public const string ThoughtForgotten = "forgotten";
        public const string ThoughtUnknown = "unknown thought";

        // The master list of all thoughts, the second tab-stop of the thought cabinet; spoken as its list
        // label when Tab enters it.
        public const string ThoughtListLabel = "all thoughts";

        // Research time, in-game hours and minutes (research advances as in-game time passes). A cooking
        // thought reads how much is left; an available thought reads how long it will take to research.
        public const string ThoughtTimeRemaining = "remaining";
        public const string ThoughtResearchTime = "research time";

        /// <summary>A cooking thought's research stage, spoken as "researching, {n} percent".</summary>
        public static string ThoughtResearching(int percent) => "researching, " + percent + " percent";

        /// <summary>An in-game duration in hours and minutes, e.g. "2 hours 15 minutes", "45 minutes",
        /// "3 hours". Zero reads as "less than a minute" (a research time that rounds to nothing left).</summary>
        public static string Duration(int minutes)
        {
            if (minutes <= 0)
                return "less than a minute";
            int h = minutes / 60, m = minutes % 60;
            var parts = new System.Collections.Generic.List<string>(2);
            if (h > 0) parts.Add(h + (h == 1 ? " hour" : " hours"));
            if (m > 0) parts.Add(m + (m == 1 ? " minute" : " minutes"));
            return string.Join(" ", parts);
        }

        // Journal. The tasks/map tabs and the active/done filter are read as the game's own tab labels
        // (TASKS, MAP, ACTIVE, DONE) plus the shared "selected" marker, so they need no authored names.

        // Task and subtask status words. DE shows a resolved task only as struck-through coloured text, with
        // no spoken word; a cancelled task reads "forfeited", a completed one "done".
        public const string JournalStatusActive = "active";
        public const string JournalStatusDone = "done";
        public const string JournalStatusCancelled = "cancelled";
        // Marks a time-limited task. DE shows it only as a clock icon.
        public const string JournalTimed = "timed";

        // The detail panel's filed/resolved time labels. The game's own resolution line reads stale for an
        // active task, so the line is composed from the model with these authored labels ("forfeited"
        // matching the game's word for a cancelled task, "completed" for a done one).
        public const string JournalFiled = "filed";
        public const string JournalCompleted = "completed";
        public const string JournalForfeited = "forfeited";

        // Found white checks (map tab) state: available to try now (the game's white state, open or
        // reopened), locked behind an unmet precondition, or only spotted in the world. DE shows these as
        // colour, with no spoken word.
        public const string JournalCheckAvailable = "available";
        public const string JournalCheckLocked = "locked";
        public const string JournalCheckSeen = "seen";

        // Quicktravel points on the map, and their state. DE draws the names into the map art and shows the
        // current spot only by a marker, so the names and state words are authored.
        public const string JournalYouAreHere = "you are here";
        public const string JournalVisited = "visited";
        public const string JournalLocChurch = "Church";
        public const string JournalLocFishingVillage = "Fishing Village";
        public const string JournalLocWaterfront = "Waterfront";

        // List labels for the journal's tab-stops, spoken when Tab enters them.
        public const string JournalTasksLabel = "tasks";
        public const string JournalTaskInfoLabel = "task info";
        public const string JournalFastTravelLabel = "fast travel locations";
        public const string JournalWhiteChecksLabel = "white checks";
        public const string JournalOfficerProfileLabel = "officer profile";

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

        // The navigable affordance that advances a conversation when there are no response choices. DE's own
        // continue control is an image with no clean text label, so the word is authored; the player reaches
        // it by pressing Down past the current line.
        public const string DialogueContinue = "continue";

        // A response that carries a skill check reads its breakdown inline after the option text: the skill
        // name and difficulty come from the game, these name the check colour (white = retryable, red =
        // one-shot) and label the trailing modifier list. The skill, difficulty, and odds lead; the modifiers
        // (the conditions that feed the check) come last.
        public const string CheckWhite = "white check";
        public const string CheckRed = "red check";
        public const string CheckModifiers = "modifiers";

        // A resolved check's silent roll line, placed in the transcript above the game's own outcome line
        // (which already speaks the skill, difficulty and success/failure). It exposes the dice and the
        // modifiers as a running sum against the base target: "<total>/<target>: rolled <d1> plus <d2>,
        // plus <skill> <name>, minus <n> <modifier>". Only these connectives are authored.
        public const string CheckRolled = "rolled";
        public const string CheckPlus = "plus";
        public const string CheckMinus = "minus";

        // Spoken when the player activates the main menu's Collage button. Collage is DE's screenshot
        // composition mode, a visual canvas with no accessible path; our navigator blocks the open and
        // says why rather than dropping the player into an unreadable screen.
        public const string CollageInaccessible = "Collage is a screenshot mode and is not accessible.";

        // The language the player taps Q/L to switch to in play (the game's internal "switchable"
        // language). It is drawn under the shared LANGUAGE header with no label of its own, so authored.
        public const string SecondaryLanguage = "secondary language";

        // Type-ahead search: spoken when clearing a live search (Escape), and the buffer text first
        // (the distinguishing part) when it matches nothing. DE has no equivalent strings, so authored.
        public const string SearchCleared = "search cleared";

        /// <summary>Spoken when the typed search buffer matches no item in the focused list.</summary>
        public static string SearchNoMatch(string buffer) => buffer + ", no match";

        // ---- Inventory ----

        // The mod's labels for the equipment-doll slots, the section/list names, and the slot/tab status
        // words. The slot captions are read live from the game's own "<slot>Tag" labels where present; these
        // are the fallbacks for when a caption is missing, and DE exposes no list/section names of its own.
        public const string InventoryEquipmentLabel = "equipped";
        public const string InventoryTabsLabel = "categories";
        public const string InventoryItemsLabel = "items";
        public const string InventoryStatsLabel = "stats";
        public const string InventoryKeys = "keys";
        public const string InventoryBullets = "bullets";
        public const string InventorySlotEmpty = "empty";
        public const string InventoryNoItems = "no items";
        public const string InventoryFresh = "new";

        // Fallback equipment-slot captions, keyed off the dock name, used only when the game's own "<slot>Tag"
        // label is missing.
        public static string EquipmentSlotName(string dockName)
        {
            switch (dockName)
            {
                case "hat": return "hat";
                case "jacket": return "jacket";
                case "shirt": return "shirt";
                case "pants": return "pants";
                case "glasses": return "glasses";
                case "neck": return "neck";
                case "gloves": return "gloves";
                case "shoes": return "shoes";
                case "heldLeft": return "held left";
                case "heldRight": return "held right";
                default: return dockName;
            }
        }

        /// <summary>An item's pawn value, spoken in the pawnables tab.</summary>
        public static string ItemValue(int value) => "value " + value;

        /// <summary>A consumable's remaining uses.</summary>
        public static string ItemUses(int uses) => uses + (uses == 1 ? " use" : " uses");

        /// <summary>A continuous slider's position as a percentage of its travel.</summary>
        public static string Percent(int value) => value + " percent";

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => "step " + index + " of " + count;
    }
}
