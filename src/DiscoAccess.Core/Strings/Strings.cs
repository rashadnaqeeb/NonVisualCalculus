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
        public const string InputModMenu = "Open mod menu";
        // The world keymap: the cursor glide, the recenter, the walk-then-interact verb, and its cancel.
        public const string InputWorldMoveNorth = "Move cursor north";
        public const string InputWorldMoveSouth = "Move cursor south";
        public const string InputWorldMoveEast = "Move cursor east";
        public const string InputWorldMoveWest = "Move cursor west";
        public const string InputWorldRecenter = "Recenter cursor on character";
        public const string InputWorldInteract = "Walk and interact";
        public const string InputWorldWalk = "Walk to cursor without interacting";
        public const string InputWorldStop = "Stop";
        // The scanner keys: each landing moves the cursor onto the thing, so Enter acts on it directly.
        public const string InputWorldScanNext = "Scanner next thing";
        public const string InputWorldScanPrev = "Scanner previous thing";
        public const string InputWorldScanNextCategory = "Scanner next category";
        public const string InputWorldScanPrevCategory = "Scanner previous category";
        public const string InputWorldScanPeopleNext = "Scanner next person or interactable";
        public const string InputWorldScanPeoplePrev = "Scanner previous person or interactable";
        public const string InputWorldScanItemsNext = "Scanner next item";
        public const string InputWorldScanItemsPrev = "Scanner previous item";
        public const string InputWorldScanExitsNext = "Scanner next exit";
        public const string InputWorldScanExitsPrev = "Scanner previous exit";
        // The world's information-screen, pause, and help keys.
        public const string InputWorldInventory = "Open inventory";
        public const string InputWorldCharacterSheet = "Open character sheet";
        public const string InputWorldJournal = "Open journal";
        public const string InputWorldThoughtCabinet = "Open thought cabinet";
        public const string InputWorldMap = "Open map";
        public const string InputWorldPause = "Pause menu";
        public const string InputWorldHelp = "Help";
        // The world's gameplay quick-actions.
        public const string InputWorldHealHealth = "Heal health";
        public const string InputWorldHealMorale = "Heal morale";
        public const string InputWorldLeftHandItem = "Use left-hand item";
        public const string InputWorldRightHandItem = "Use right-hand item";
        public const string InputWorldQuickSave = "Quick save";
        public const string InputWorldQuickLoad = "Quick load";
        public const string InputWorldLanguage = "Cycle language";
        // The world's status-readout keys.
        public const string InputWorldReadTime = "Read time";
        public const string InputWorldReadMoney = "Read money";
        public const string InputWorldReadHealth = "Read health";
        public const string InputWorldReadLocation = "Read location";

        // Control role words, spoken after a control's label so the user knows what it is.
        public const string RoleButton = "button";
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

        // The twelve thought slots, the first tab-stop of the thought cabinet; spoken as its grid label
        // when Tab enters it.
        public const string ThoughtSlotGridLabel = "grid";

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
        // The world's loot panel (opened by interacting with an unlocked container). It has no ViewType of
        // its own (the view stays CLEAR) and no game title, so its name is authored.
        public const string ScreenContainer = "container";
        // The endgame newspaper, the full-screen article shown after a death or a game ending. The game
        // titles it only with the article's own headline, so its name is authored.
        public const string ScreenNewspaper = "newspaper";
        // The finished-thought splash, shown when a thought completes internalization. Its only game
        // text is the thought itself (the view has no title term), so its name is authored.
        public const string ScreenThoughtComplete = "thought complete";
        // The full-screen begin prompt (a black screen with one BEGIN button) a new game or a dream
        // sequence waits on before its opening dialogue. The game's caption is baked into a localized
        // sprite (its I2 term resolves to the sprite name, not text), so the prompt is authored; it is
        // the screen's whole announcement, with the button itself silent (see BeginScreen).
        public const string ScreenBeginPrompt = "Press Enter to begin";

        // The mod's own settings menu, opened with F12. It maps to no game view, so its name is authored.
        public const string ScreenModMenu = "mod menu";

        // Mod settings labels, spoken as the setting's name in the mod menu. Authored: these are the mod's
        // own options, with no game string to read.
        public const string SettingAutoReadDialogue = "Automatically read dialogue";
        public const string SettingReadAmbientDialogue = "Read ambient dialogue";
        public const string SettingWallToneVolume = "Wall tone volume";
        public const string SettingWallTonesContinuous = "Continuous wall tones";
        // One volume for the sonar sweep's pings and the scanner's review ping - the same sounds through
        // the same falloff, so one knob.
        public const string SettingSonarVolume = "Sonar volume";
        public const string SettingSonarContinuous = "Continuous sonar";
        public const string SettingSonarRest = "Time between sonar sweeps";
        // The sonar's per-category toggles, built from the scanner's spoken browse-group words (WorldScan*)
        // so the menu and the scanner call a category the same thing.
        public const string SettingSonarNpcs = "Sonar " + WorldScanNpcs;
        public const string SettingSonarInteractables = "Sonar " + WorldScanInteractables;
        public const string SettingSonarContainers = "Sonar " + WorldScanContainers;
        public const string SettingSonarOrbs = "Sonar " + WorldScanOrbs;
        public const string SettingSonarExits = "Sonar " + WorldScanExits;
        public const string SettingRunToDestinations = "Run to destinations";

        // The navigable affordance that advances a conversation when there are no response choices. DE's own
        // continue control is an image with no clean text label, so the word is authored; the player reaches
        // it by pressing Down past the current line.
        public const string DialogueContinue = "continue";

        // The endgame newspaper's article-paging arrows, image-only buttons with no caption term to read.
        public const string NewspaperNextArticle = "next article";
        public const string NewspaperPreviousArticle = "previous article";

        // A response that carries a skill check reads its breakdown inline after the option text: the skill
        // name and difficulty come from the game, these name the check colour (white = retryable, red =
        // one-shot) and label the trailing modifier list. The skill, difficulty, and odds lead; the modifiers
        // (the conditions that feed the check) come last.
        public const string CheckWhite = "white check";
        public const string CheckRed = "red check";
        public const string CheckModifiers = "modifiers";

        // Leads the player's effective total on an unrolled check (skill plus bonuses, modifiers folded
        // in), read after the odds: "your 3". The target it is measured against is game text - the
        // label's own check tag, or the difficulty-and-target pair when the tag is hidden.
        public const string CheckYours = "your";

        // Fallbacks for the game's cost-option tooltip terms (TOOLTIP_COST / TOOLTIP_YOU_HAVE), used
        // only when a term fails to resolve; the game's words are preferred so they localize.
        public const string CostWord = "cost";
        public const string CostYouHave = "you have";

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

        // Fallback for the game's TOOLTIP_HOVER_SUBSTANCE_USED_EFFECTS header labelling a substance's
        // effects-when-used, used only when the term fails to resolve; the game's word is preferred.
        public const string ItemUseEffects = "effects when used";

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

        /// <summary>A duration slider's value in milliseconds.</summary>
        public static string Milliseconds(int value) => value + " milliseconds";

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => "step " + index + " of " + count;

        // ---- World navigation ----

        // The world cursor's spatial readout: an eight-point compass bearing, a distance in metres, and a
        // vertical offset, all relative to the player. These are the mod's own world-cursor controls with no
        // DE string to read, so they are authored. Distances are in metres (Disco's 1 unit = 1 metre scale).
        private static readonly string[] WorldCompassWords =
            { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };

        /// <summary>An eight-point compass word for a bearing index 0..7 (0 = north), or empty when out of
        /// range (the coincident "here" case, which the readout handles separately).</summary>
        public static string WorldCompass(int index)
            => index >= 0 && index < WorldCompassWords.Length ? WorldCompassWords[index] : "";

        /// <summary>A whole-metre distance, e.g. "3 meters", "1 meter"; under a metre reads "less than a
        /// meter" so a near-but-not-coincident point never reads "0 meters".</summary>
        public static string WorldDistance(int meters)
            => meters <= 0 ? "less than a meter" : meters == 1 ? "1 meter" : meters + " meters";

        public const string WorldHere = "here";
        public const string WorldAbove = "above";
        public const string WorldBelow = "below";

        // The world sensing systems' names, spoken in the settings menu. Authored (the mod's own systems).
        public const string WorldSystemSpatial = "cursor position";
        public const string WorldSystemWallTones = "wall tones";
        public const string WorldSystemObjectCue = "cursor objects";
        public const string WorldSystemSonar = "sonar";

        // The scanner's spoken category names (the review cursor's browse groups) and its list readouts.
        // Authored: the taxonomy is the mod's own grouping, so the game has no label for it. Plural, since a
        // category is a set ("exits, 3").
        public const string WorldScanEverything = "everything";
        public const string WorldScanNpcs = "people";
        public const string WorldScanInteractables = "interactables";
        public const string WorldScanContainers = "containers";
        public const string WorldScanOrbs = "orbs";
        public const string WorldScanExits = "exits";
        // The quick-nav groups' labels, spoken only when a group key finds nothing ("items, none"); exits
        // reuse the category word.
        public const string WorldScanPeopleGroup = "people and interactables";
        public const string WorldScanItemsGroup = "items";

        /// <summary>A scan category's landing line: its label and how many things are in it ("exits, 3";
        /// "orbs, none"). Label first, the distinguishing part.</summary>
        public static string WorldScanCategoryCount(string label, int count)
            => count == 0 ? label + ", none" : label + ", " + count;

        // Generic type words: the spoken name for a thing whose own name is a slug and has no spoiler-safe
        // title to fall back to (see EntityNaming). A door reads "door", a crate "container", and so on, so
        // nothing the cursor passes over goes nameless. Authored (the game has no short type label).
        public const string WorldThingDoor = "door";
        public const string WorldThingGate = "gate";
        public const string WorldThingStairs = "stairs";
        public const string WorldThingElevator = "elevator";
        public const string WorldThingLadder = "ladder";
        public const string WorldThingBoat = "boat";
        public const string WorldThingExit = "exit";
        // Authored names for specific dev-named doors the game gives no examine actor and no readable
        // object name (see EntityNaming's door fallback table): the raw names leak cast names ("Whirling
        // Door Bathroom Klaasje") or read as slugs, and no game string exists to reuse.
        public const string WorldThingBathroomDoor = "bathroom door";
        // The door joining the Whirling's shared bathroom to Kitsuragi's room: two doors on one bathroom
        // both reading "bathroom door" are indistinguishable by ear, so the one that crosses into the
        // neighbouring room reads as the connection it is.
        public const string WorldThingConnectingDoor = "connecting door";
        public const string WorldThingLockedDoor = "locked door";
        // Level words for an exit whose destination floor shares its building's name (see
        // EntityNaming.ExitDestinationLabel): "floor 2 stairs", "basement stairs".
        public const string WorldFloor = "floor";
        public const string WorldBasement = "basement";
        public const string WorldThingContainer = "container";
        // The container token "buoya" is the internal spelling of the numbered coastal buoy "buoy A";
        // spoken so the letter reads as itself, not "buoya". Authored (the game keeps only the slug form).
        public const string WorldBuoyA = "buoy A";
        public const string WorldThingPerson = "person";
        public const string WorldThingOrb = "orb";
        public const string WorldThingObject = "object";

        // Authored names for the map's sub-districts (Martinaise-ext), spoken as the player crosses into one
        // and appended to the map name by the location readout. The game has no runtime sub-district names -
        // it names whole scenes only - so the mod authors the partition (see the module's DistrictReader).
        public const string DistrictPlaza = "Plaza";
        public const string DistrictYard = "Yard";
        public const string DistrictTrafficJam = "Traffic Jam";
        public const string DistrictHarbourGate = "Harbour Gate";
        public const string DistrictHarbour = "Harbour";
        public const string DistrictPier = "Pier";
        public const string DistrictWaterlock = "Waterlock";
        public const string DistrictFishingVillage = "Fishing Village";
        public const string DistrictIce = "Ice";
        public const string DistrictFishMarket = "Fish Market";
        public const string DistrictLandsEnd = "Land's End";
        public const string DistrictBoardwalk = "Boardwalk";
        public const string DistrictSeaFortress = "Sea Fortress";
        public const string DistrictWhirlingBalcony = "Whirling Balcony";
        public const string DistrictApartmentBalcony = "Apartment Balcony";
        public const string DistrictApartmentRoof = "Apartment Roof";

        /// <summary>The location readout ('r'): the localized map name, then the sub-district when there is
        /// one ("Martinaise, Harbour"; just "Whirling in Rags floor 1" indoors). The map name is the game's
        /// own localized area name, so only the join is authored here.</summary>
        public static string WorldLocation(string map, string subregion)
            => string.IsNullOrEmpty(subregion) ? map : map + ", " + subregion;

        // The walk-then-interact verb's spoken feedback. The mod authors these (DE has no equivalent line):
        // committing a move, the bare-ground move with no target, its arrival, the reachability refusal,
        // and the cancel.

        /// <summary>Spoken on committing the walk-then-interact verb toward a named target.</summary>
        public static string WorldMovingTo(string name)
            => string.IsNullOrEmpty(name) ? WorldMoving : "moving to " + name;

        /// <summary>Spoken on moving to a bare-ground spot with no target.</summary>
        public const string WorldMoving = "moving";

        /// <summary>Spoken on arriving at a bare-ground spot (a targeted move ends in its interaction,
        /// whose own readers speak instead).</summary>
        public const string WorldArrived = "arrived";

        /// <summary>Spoken when the target cannot be pathed to from where the character currently stands.</summary>
        public static string WorldUnreachable(string name)
            => string.IsNullOrEmpty(name) ? "can't reach" : name + ", can't reach";

        /// <summary>Spoken when the player cancels a committed walk.</summary>
        public const string WorldStopped = "stopped";

        /// <summary>Spoken when a walk is refused because a paralyzer or unresolved thought orb holds the
        /// character in place (the game's own movement block); the orb rides the character and must be
        /// triggered to release it.</summary>
        public const string WorldOrbHolds = "held by an orb, resolve it to move";

        // ---- The world's loot panel (see the module's ContainerPanelScreen / ContainerReader) ----

        /// <summary>Spoken when the loot panel closes, whatever closed it (the Close button, taking the
        /// last item, Take all, or walking out of range) - the panel is silent otherwise and a blind
        /// player must hear that the list they were arrowing through is gone.</summary>
        public const string WorldContainerClosed = "container closed";

        /// <summary>Spoken when interacting with a locked container the game refuses to open (it plays
        /// only a rattle sound). Fallback for when the game's own "Locked" tooltip term fails to
        /// resolve; the term is preferred so the word localizes.</summary>
        public const string StatusLocked = "locked";

        // ---- World status readouts (mod-authored; the game has no spoken equivalent) ----

        // The wallet total. The game stores money in centims (100 = one réal) and its on-screen formatter
        // prefixes a réal glyph the reader cannot speak, so the readout is composed here from the raw value.
        public static string WorldMoney(int centims)
            => (centims / 100) + "." + (centims % 100).ToString("D2") + " réal";

        // The two health bars (the game's own Health and Morale, not the Endurance/Volition skills that set
        // their maximums), each current of maximum, plus the count of assigned healing charges. The bar names
        // are passed in from the game so they localize; the rest is composed here.
        public static string WorldHealth(string healthName, int healthCurrent, int healthMax, int healthCharges,
                                         string moraleName, int moraleCurrent, int moraleMax, int moraleCharges)
            => healthName + " " + healthCurrent + "/" + healthMax + ", " + HealCharges(healthCharges)
             + "; " + moraleName + " " + moraleCurrent + "/" + moraleMax + ", " + HealCharges(moraleCharges);

        /// <summary>A count of assigned healing charges, singular/plural.</summary>
        public static string HealCharges(int count) => count == 1 ? "1 healing charge" : count + " healing charges";

        // ---- World quick-action feedback (mod-authored; the game speaks none of these). The heal feedback
        // is composed around the game's bar name (Health/Morale) so it localizes. ----
        public static string WorldBarHealed(string barName) => barName + " healed";
        public static string WorldBarFull(string barName) => barName + " full";
        public static string WorldNoBarHeal(string barName) => "no " + barName + " items";
        public const string WorldUsedLeftHand = "used left hand item";
        public const string WorldUsedRightHand = "used right hand item";
        public const string WorldLeftHandEmpty = "left hand empty";
        public const string WorldRightHandEmpty = "right hand empty";
        public const string WorldQuickLoading = "quick loading";
        public const string WorldNoQuickSave = "no quick save";
        public const string WorldLanguageChanged = "language changed";

        // ---- Existential crisis: a bar (Health or Morale) hit zero and the game paused for a heal-or-die
        // window (a 10-second grace), so this is the one notification spoken with interrupt. The bar name and
        // gameMessage are the game's own localized strings; the heal-key hint is authored, because the heal
        // window is the unusual, timed control that justifies a key hint (Health heals with Left, Morale with
        // Right, matching the heal keys). ----
        public static string CrisisHeal(string barName, bool healWithLeft, string? gameMessage)
        {
            string prompt = barName + " critical, press " + (healWithLeft ? "left" : "right") + " arrow to heal";
            return string.IsNullOrEmpty(gameMessage) ? prompt : prompt + ". " + gameMessage;
        }
    }
}
