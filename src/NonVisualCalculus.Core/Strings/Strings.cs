using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NonVisualCalculus.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read
    /// live and already localized). Every authored word lives in <see cref="Defaults"/> as a key and
    /// its English value; the members below are typed accessors reading through <see cref="Translation"/>,
    /// so a loaded translation file overrides any value at speak time. Grammar the accessors used to
    /// hardcode lives in the values instead: "{0}"-style slots carry word order, and '|'-separated
    /// forms carry plurals (picked by the translation's plural rule - see <see cref="PluralRules"/>).
    /// Game-content reading must never route through here.
    /// </summary>
    public static class Strings
    {
        private static KeyValuePair<string, string> D(string key, string value)
            => new KeyValuePair<string, string>(key, value);

        /// <summary>Every authored string, in template order: the key a translation file addresses and
        /// its English default. The source of truth <see cref="DumpTemplate"/> renders for translators.
        /// The comments in this table are the translation context: a lang/&lt;language&gt;.txt file is
        /// produced by reading this file directly, so each entry carries what its translator needs -
        /// where the line is spoken, what fills each {0} slot, and the part of speech where a bare
        /// English word is ambiguous. "Match I2 TERM" means the game itself localizes that term: fetch
        /// its value in the target language (the live game's I2 sources, or
        /// GameLocalization.Translate) and reuse its vocabulary rather than inventing a new word.
        /// Values are spoken by a screen reader: terse, lowercase unless shown otherwise, no
        /// decorative punctuation.
        /// Where a value is spoken appended after a noun the RUNTIME chooses - an item's name, an
        /// equipment slot's caption, a health bar's name, a scanner category word - a gendered
        /// language cannot inflect it to agree, because the noun varies. Such entries say so; give
        /// them a phrase carrying its own head noun ("a new item") or an invariant one ("at a
        /// critical level"), never a bare adjective.</summary>
        internal static readonly KeyValuePair<string, string>[] Defaults =
        {
            // Spoken once when the mod finishes initializing at game launch; {0} = the mod version
            // (e.g. "1.0.0"). The mod name negates the game's Visual Calculus skill: build it from
            // the game's own localization of that skill name, negated the way the language naturally
            // negates an adjective, so a player who knows the game recognizes the reference.
            D("ModLoaded", "Non-Visual Calculus {0} loaded"),
            // Spoken at launch instead when the feature module fails to load (the mod is then dead).
            // Same mod-name rule as ModLoaded.
            D("ModuleFailed", "Non-Visual Calculus features failed to load"),
            // Spoken once, shortly after the load line, when a newer mod release exists online;
            // {0} = the new version ("1.1.0"). "the installer" = the mod's own installer app, which
            // the player already used to install the mod and which also updates it - match the word
            // that installer's UI uses for itself in this language. Same mod-name rule as ModLoaded.
            D("UpdateAvailable", "Non-Visual Calculus update {0} available, run the installer to update"),
            // Spoken at launch when the mod's old release, named Whirling in Words, is still installed
            // beside this one (both then speak every line twice). "Whirling in Words" is that old
            // release's name: reuse the old ModLoaded name for this language. The folder name
            // WhirlingInWords is a literal file path, never translated.
            D("LegacyModStillInstalled",
                "The old Whirling in Words version is also installed and everything will speak twice. "
                + "Delete the WhirlingInWords folder from BepInEx plugins in the game folder."),

            // Input action descriptions: the human-readable name of each bound action, for a keybindings
            // reader to speak. DE names none of these (they are our own controls), so they are authored.
            // Short imperative phrases. Mod jargon: the "cursor" is the mod's movable point in the world
            // scene, the "scanner" its cycling list of nearby things, and a "thing" any world object.
            D("InputNavigateUp", "Navigate up"),
            D("InputNavigateDown", "Navigate down"),
            D("InputNavigateLeft", "Navigate left"),
            D("InputNavigateRight", "Navigate right"),
            D("InputNextControl", "Next control"),
            D("InputPrevControl", "Previous control"),
            D("InputActivate", "Activate control"),
            // Go back / cancel out of the current screen (Escape).
            D("InputBack", "Back"),
            // Jump to the first / last entry of the focused list (Home / End).
            D("InputJumpFirst", "Jump to first"),
            D("InputJumpLast", "Jump to last"),
            // The focused control's secondary action (Backspace), named for its main use: acting on
            // the focused item (equip, use, take). Match the interact verb of InputWorldInteract.
            D("InputSecondary", "Interact with item"),
            // Cycle the open info screen to the previous/next one (character sheet, inventory, journal,
            // thought cabinet) - the game's own controller trigger cycle, re-provided.
            D("InputScreenPrev", "Previous game screen"),
            D("InputScreenNext", "Next game screen"),
            D("InputModMenu", "Open mod menu"),
            D("InputBookmarks", "Open bookmarks menu"),
            D("InputWorldMoveNorth", "Move cursor north"),
            D("InputWorldMoveSouth", "Move cursor south"),
            D("InputWorldMoveEast", "Move cursor east"),
            D("InputWorldMoveWest", "Move cursor west"),
            D("InputWorldRecenter", "Recenter cursor on character"),
            D("InputWorldInteract", "Walk and interact"),
            D("InputWorldWalk", "Walk to cursor without interacting"),
            D("InputWorldStop", "Stop character"),
            D("InputWorldScanNext", "Scanner next thing"),
            D("InputWorldScanPrev", "Scanner previous thing"),
            D("InputWorldScanNextCategory", "Scanner next category"),
            D("InputWorldScanPrevCategory", "Scanner previous category"),
            D("InputWorldScanInteract", "Walk and interact with scanned thing"),
            D("InputWorldScanPeopleNext", "Scanner next person or interactable"),
            D("InputWorldScanPeoplePrev", "Scanner previous person or interactable"),
            D("InputWorldScanItemsNext", "Scanner next item"),
            D("InputWorldScanItemsPrev", "Scanner previous item"),
            D("InputWorldScanExitsNext", "Scanner next exit"),
            D("InputWorldScanExitsPrev", "Scanner previous exit"),
            D("InputWorldScanCursor", "Move cursor to scanned thing"),
            D("InputWorldScanWaypoint", "Walking direction to scanned thing"),
            D("InputWorldInventory", "Open inventory"),
            D("InputWorldCharacterSheet", "Open character sheet"),
            D("InputWorldJournal", "Open journal"),
            D("InputWorldThoughtCabinet", "Open thought cabinet"),
            D("InputWorldMap", "Open map"),
            D("InputWorldPause", "Pause menu"),
            D("InputWorldHelp", "Help"),
            // "Health" and "morale" are the game's two damage bars: match I2 HEALTH / MORALE.
            D("InputWorldHealHealth", "Heal health"),
            D("InputWorldHealMorale", "Heal morale"),
            D("InputWorldLeftHandItem", "Use left-hand item"),
            D("InputWorldRightHandItem", "Use right-hand item"),
            D("InputWorldQuickSave", "Quick save"),
            D("InputWorldQuickLoad", "Quick load"),
            // The game's language quick-switch: swaps between the primary and secondary display
            // language configured in options (the mod's speech and strings follow it).
            D("InputWorldLanguage", "Switch language"),
            D("InputWorldReadTime", "Read time"),
            D("InputWorldReadMoney", "Read money"),
            D("InputWorldReadHealth", "Read health"),
            D("InputWorldReadLocation", "Read location"),
            D("InputWorldReadExperience", "Read experience"),
            // Opens the mod's key-help screen: the list of every key live in the current context.
            D("InputKeyHelp", "Key help"),
            // Dialogue viewer only: a number-row digit moves the cursor to that numbered response.
            // Spoken as a key-help line with KeyHelpKeysDigits as its keys phrase.
            D("InputDialogueNumberJump", "Jump to response by number"),

            // The key-help screen's lines. One line per reachable action: {0} the action's description
            // (the Input* strings above), {1} its key or keys ("Read time, T").
            D("KeyHelpLine", "{0}, {1}"),
            // Spoken names of keyboard keys and modifiers, composed into a chord with spaces
            // ("Control Page down"). Use the words a screen-reader user knows the keys by.
            D("KeyControl", "Control"),
            D("KeyShift", "Shift"),
            D("KeyAlt", "Alt"),
            D("KeyUpArrow", "Up arrow"),
            D("KeyDownArrow", "Down arrow"),
            D("KeyLeftArrow", "Left arrow"),
            D("KeyRightArrow", "Right arrow"),
            D("KeyReturn", "Enter"),
            D("KeyKeypadEnter", "Numpad enter"),
            D("KeyEscape", "Escape"),
            D("KeyBackspace", "Backspace"),
            D("KeyTab", "Tab"),
            D("KeyHome", "Home"),
            D("KeyEnd", "End"),
            D("KeySpace", "Space"),
            D("KeyComma", "Comma"),
            D("KeyPeriod", "Period"),
            D("KeySlash", "Slash"),
            D("KeyPageUp", "Page up"),
            D("KeyPageDown", "Page down"),
            // Key-help groups: related keys that read as one line instead of several. The group label
            // fills KeyHelpLine's {0} slot; the two direction fans also author its keys phrase, the {1}
            // slot ("Navigate, arrow keys"), while the pair groups below derive theirs from the live
            // bindings ("Jump to first or last, Home, End").
            D("KeyHelpGroupNavigate", "Navigate"),
            D("KeyHelpKeysArrows", "arrow keys"),
            D("KeyHelpGroupMoveCursor", "Move cursor"),
            D("KeyHelpKeysWasd", "W A S D"),
            // The dialogue number jump's keys phrase (the number-row range).
            D("KeyHelpKeysDigits", "1 to 9"),
            D("KeyHelpGroupNextPrevControl", "Next and previous control"),
            D("KeyHelpGroupJumpFirstLast", "Jump to first or last"),
            D("KeyHelpGroupScanThing", "Scanner next and previous thing"),
            D("KeyHelpGroupScanCategory", "Scanner next and previous category"),
            D("KeyHelpGroupScanPeople", "Scanner people and interactables"),
            D("KeyHelpGroupScanItems", "Scanner items"),
            D("KeyHelpGroupScanExits", "Scanner exits"),

            // Control role and status words.
            // Nouns naming a control's type, spoken after its caption ("Continue, button").
            D("RoleButton", "button"),
            // A UI tab (a tabbed page's selector), not the Tab key.
            D("RoleTab", "tab"),
            // Adjective marking the tab or entry whose content is currently shown; the mod-wide
            // standard word for that state.
            D("StatusSelected", "selected"),
            // Noun phrase prefixed to the save menu's create-new-save slot, so activating it reads as
            // a new save, not an overwrite.
            D("StatusNewSave", "new save"),
            // Verb: the save menu's rename affordance; activating it starts editing the save's name.
            D("ActionRename", "rename"),
            // Spoken when a text field starts editing (keystrokes now type into the field).
            D("StatusEditMode", "edit mode"),
            // Nouns naming a setting's control type, spoken after the setting's name.
            D("ControlSlider", "slider"),
            D("ControlToggle", "toggle"),
            D("ControlDropdown", "dropdown"),
            // A toggle's two states.
            D("StatusOn", "on"),
            D("StatusOff", "off"),
            // Marks the character's signature skill on the sheet; match I2 SIGNATURE_SKILL_LABEL
            // ("Signature Skill").
            D("StatusSignature", "signature"),
            // Verb phrase marking a skill a point can be spent on right now.
            D("StatusCanRaise", "can raise"),
            // {0} = count of unspent skill points; the character sheet's pool readout.
            D("SkillPoints", "{0} skill point|{0} skill points"),
            // Full clause, spoken when raising an ability is refused because the point pool is empty.
            D("AbilityNoPointsLeft", "no more points to assign"),
            // Adjectives: the three notches of the game's Menu Size / Dialogue Text Size sliders.
            D("StepSmall", "small"),
            D("StepMedium", "medium"),
            D("StepLarge", "large"),
            // Nouns, spoken when a slider is pushed past its end, so the user hears that it stopped.
            D("StatusMinimum", "minimum"),
            D("StatusMaximum", "maximum"),

            // Thought cabinet: the game's screen where discovered thoughts are placed into slots and
            // researched over in-game time (match its vocabulary, I2 THC_BASIC_VIEW_TITLE "Thought
            // Cabinet"). These states are game enums shown only as art, so the words are authored.
            // A free slot a thought can be placed into.
            D("ThoughtSlotEmpty", "empty slot"),
            // A locked slot the player can unlock right now by spending a skill point.
            D("ThoughtSlotUnlockable", "locked slot, can unlock"),
            // A locked slot that cannot be unlocked yet.
            D("ThoughtSlotLocked", "locked slot"),
            // A discovered thought not yet placed for research; adjective.
            D("ThoughtAvailable", "available"),
            // Research finished, the thought's effects are permanent; adjective.
            D("ThoughtResearched", "researched"),
            // A thought removed with the game's Forget action (match I2 Buttons/THC_BUTTON_FORGET_TEXT
            // "Forget"); adjective.
            D("ThoughtForgotten", "forgotten"),
            // Placeholder name for a thought whose state cannot be read.
            D("ThoughtUnknown", "unknown thought"),
            // The slot grid section's label, spoken when Tab enters it; "grid" the layout noun.
            D("ThoughtSlotGridLabel", "grid"),
            // The list of every discovered thought, the screen's other Tab-stop.
            D("ThoughtListLabel", "all thoughts"),
            // The research time left on a cooking thought; {0} = a duration phrase built from the
            // Duration* keys below ("2 hours remaining"). Place the slot where the language wants it.
            D("ThoughtTimeRemaining", "{0} remaining"),
            // How long an unplaced thought will take to research; {0} = the same duration phrase
            // ("research time 3 hours"). Place the slot where the language wants it.
            D("ThoughtResearchTime", "research time {0}"),
            // A thought mid-research; {0} = whole-number percent complete.
            D("ThoughtResearching", "researching, {0} percent"),
            // In-game durations. A duration with both parts joins the two below; {0} = the hours
            // phrase, {1} = the minutes phrase. A language that writes no space between them (the CJK
            // ones) drops it here rather than carrying the English one.
            D("DurationZero", "less than a minute"),
            D("DurationHours", "{0} hour|{0} hours"),
            D("DurationMinutes", "{0} minute|{0} minutes"),
            D("DurationHoursMinutes", "{0} {1}"),

            // Journal: the game's task log plus its map tab.
            // Task states (DE shows a resolved task only as struck-through text). Adjectives.
            D("JournalStatusActive", "active"),
            D("JournalStatusDone", "done"),
            // Match I2 JOURNAL_TASK_CANCELLED ("Cancelled").
            D("JournalStatusCancelled", "cancelled"),
            // Marks a time-limited task (DE shows only a clock icon); adjective.
            D("JournalTimed", "timed"),
            // Labels spoken before an in-game timestamp in the task detail panel: when the task was
            // added, and when it was resolved either way. The game's own words are I2 TASK_FILLED
            // ("Filled"), TASK_RESOLVED ("COMPLETED"), TASK_FORFEITED ("Forfeited").
            D("JournalFiled", "filed"),
            D("JournalCompleted", "completed"),
            D("JournalForfeited", "forfeited"),
            // A found white check's state on the map tab (DE shows these as colour only): available =
            // can be attempted now, locked = its precondition is unmet, seen = only spotted in the
            // world, not yet engaged. Adjectives.
            D("JournalCheckAvailable", "available"),
            D("JournalCheckLocked", "locked"),
            D("JournalCheckSeen", "seen"),
            // The quicktravel point the player is standing at; match I2 QUICKTRAVEL_YOU_ARE_HERE
            // ("You are here").
            D("JournalYouAreHere", "you are here"),
            // A quicktravel point already discovered, so it can be travelled to; adjective.
            D("JournalVisited", "visited"),
            // The three quicktravel point names, drawn into the map art; match the game's own localized
            // names for these places, not a fresh translation: I2 ANNOTATION_QUICKTRAVEL_CHURCH
            // ("Church"), ANNOTATION_QUICKTRAVEL_FISHING_VILLAGE ("Fisherman Shacks"), and
            // Buttons/JOURNAL_MAP_WATERFRONT_TEXT ("MARTINAISE WATERFRONT").
            D("JournalLocChurch", "Church"),
            D("JournalLocFishingVillage", "Fishing Village"),
            D("JournalLocWaterfront", "Waterfront"),
            // Labels for the journal's Tab-stops, spoken when Tab enters each section.
            // The task list; match I2 JOURNALS_TASKS_TAB ("Tasks").
            D("JournalTasksLabel", "tasks"),
            // The detail panel for the selected task.
            D("JournalTaskInfoLabel", "task info"),
            // The map tab's list of quicktravel destinations.
            D("JournalFastTravelLabel", "fast travel locations"),
            // The map tab's list of found white checks (retryable skill checks); match the check
            // vocabulary of I2 TOOLTIP_TUTO_CHECK_WHITE_OPEN ("White Check").
            D("JournalWhiteChecksLabel", "white checks"),
            // Match I2 JOURNAL_OFFICER_PROFILE_TITLE ("Officer profile").
            D("JournalOfficerProfileLabel", "officer profile"),

            // Screen names, spoken once when a screen opens; noun phrases. Where the game titles the
            // same screen, match its word (noted per key).
            // The in-game world view - no menu open, walking around. "Map" is the mod's player-facing
            // word for it; spoken when the keyboard lands back on the map controls (a menu, popup, or
            // conversation closed, or a cutscene returned control).
            D("ScreenMap", "map"),
            // Match I2 F1_SCREEN_I_KEY ("INVENTORY").
            D("ScreenInventory", "inventory"),
            // The inventory's worn-clothing view.
            D("ScreenClothing", "clothing"),
            // Match I2 THC_BASIC_VIEW_TITLE ("Thought Cabinet").
            D("ScreenThoughtCabinet", "thought cabinet"),
            // Match I2 JOURNALS_HEADER ("Journal").
            D("ScreenJournal", "journal"),
            // Match I2 CHARSHEET_CHARACTER_VIEW_TITLE ("Character sheet").
            D("ScreenCharacterSheet", "character sheet"),
            // New-game character creation: picking a premade archetype; match the vocabulary of I2
            // Archetypes/ARCHETYPE_SELECT_TITLE ("Select archetype").
            D("ScreenArchetypeSelection", "archetype selection"),
            // Character creation's custom stat-allocation step (Intellect/Psyche/Physique/Motorics).
            D("ScreenAdjustAbilities", "adjust abilities"),
            // Character creation's signature-skill picker; match I2 SIGNATURE_SKILL_LABEL
            // ("Signature Skill").
            D("ScreenSignatureSkill", "signature skill"),
            D("ScreenOptions", "options"),
            D("ScreenSave", "save game"),
            D("ScreenLoad", "load game"),
            // The title screen's menu.
            D("ScreenMainMenu", "main menu"),
            // The in-game Escape menu.
            D("ScreenPauseMenu", "pause menu"),
            D("ScreenHelp", "help"),
            // The popup showing one thought's detail.
            D("ScreenThought", "thought"),
            // The game's Collage screenshot mode (its main menu button); "collage" is the game's word.
            D("ScreenCollage", "collage mode"),
            // The loot panel shown when opening a container in the world.
            D("ScreenContainer", "container"),
            // The endgame newspaper the credits offer.
            D("ScreenNewspaper", "newspaper"),
            // The pawnshop's buy/sell trade screen.
            D("ScreenPawnshop", "pawnshop"),
            // The splash when a thought finishes research; match I2 THC_SPLASH_SCREEN_TITLE
            // ("Thought complete").
            D("ScreenThoughtComplete", "thought complete"),
            // The title screen's initial press-a-key prompt; a full sentence, "Enter" the key name.
            D("ScreenBeginPrompt", "Press Enter to begin"),
            // The mod's own settings menu.
            D("ScreenModMenu", "mod menu"),
            // The mod's own bookmarks menu (named world positions; Ctrl+B).
            D("ScreenBookmarks", "bookmarks"),
            // The mod's own key-help screen (Shift+F1): the keys live in the current context.
            D("ScreenKeyHelp", "key help"),
            // The mod's own learn-game-sounds menu, reached from the pause menu (its title, and the
            // pause-menu button that opens it): each entry plays one of the mod's audio cues on demand.
            D("ScreenLearnSounds", "learn game sounds"),

            // The learn-sounds menu's two sections: the cues the gliding cursor itself makes, and the
            // per-category pings the sonar sweep and the scanner share (those entries reuse the
            // WorldScan* category words).
            D("LearnSoundsCursorSection", "cursor sounds"),
            D("LearnSoundsSonarSection", "sonar sounds"),
            // The cursor cue entries: what event each sound marks, phrased as the cursor's motion.
            // The cursor slid onto a thing (a rising click), and off it to bare ground (a falling one).
            D("SoundCursorEnter", "onto an object"),
            D("SoundCursorExit", "off an object"),
            // The bump when a glide is refused at the edge of the sensed area.
            D("SoundCursorImpassable", "blocked at the edge"),
            // The unrestricted cursor crossing out past the sensed area, and coming back inside.
            D("SoundCursorFogEnter", "out past the senses"),
            D("SoundCursorFogExit", "back into the senses"),
            // The sonar's open-door variant of the exits cue; "open" as in standing open, not locked.
            D("SoundOpenDoor", "open door"),

            // Mod settings labels (rows in the mod menu; sentence case). "Wall tones" are the mod's
            // audio cue for walls near the cursor; the "sonar" is its periodic audio sweep over nearby
            // things.
            D("SettingAutoReadDialogue", "Automatically read dialogue"),
            // Ambient dialogue: incidental background lines characters say unprompted.
            D("SettingReadAmbientDialogue", "Read ambient dialogue"),
            D("SettingWallToneVolume", "Wall tone volume"),
            // "Continuous" = the cue keeps sounding while idle, rather than only when moving.
            D("SettingWallTonesContinuous", "Continuous wall tones"),
            D("SettingSonarVolume", "Sonar volume"),
            D("SettingSonarContinuous", "Continuous sonar"),
            D("SettingSonarRest", "Time between sonar sweeps"),
            // The sonar's per-category toggles; {0} = a scanner category word (the WorldScan* keys), so
            // the menu and the scanner call a category the same thing.
            D("SettingSonarCategory", "Sonar {0}"),
            // Toggle: the scanner speaks each thing's direction and distance measured from the cursor's
            // position instead of the character's. "Readouts" = those spoken direction-and-distance lines.
            D("SettingScannerFromCursor", "Scanner readouts from cursor"),
            // Toggle: the character runs instead of walking when sent somewhere.
            D("SettingRunToDestinations", "Run to destinations"),
            // Toggle, a testing aid: the cursor may leave the visible frame and enter fog-of-war ground
            // instead of stopping at those edges.
            D("SettingUnrestrictCursor", "Unrestricted cursor"),
            // Toggle: at launch, look up the mod's newest release online and speak UpdateAvailable if
            // the installed mod is older (up to date stays silent).
            D("SettingCheckForUpdates", "Check for updates at launch"),

            // Dialogue and checks.
            // The control that advances a conversation when there are no response choices; verb.
            // Match the game's Continue button caption (I2 Buttons/ARCHETYPE_BUTTON_CONTINUE_TEXT).
            D("DialogueContinue", "continue"),
            // Suffix on a response the player has already picked before (its node was displayed): the game
            // dims such an option's text, so we speak this after the option and its breakdown. Only a chosen
            // response earns it - an option merely offered before but never taken reads plain, as it draws.
            D("DialogueAlreadyChosen", "already chosen"),
            // A response activation was refused because the game's own button is not accepting clicks
            // yet (the menu still fading in, an animation holding it); pressing again a moment later
            // works, so this only marks the early press.
            D("DialogueNotReady", "not ready"),
            // The endgame newspaper's article-paging arrow buttons.
            D("NewspaperNextArticle", "next article"),
            D("NewspaperPreviousArticle", "previous article"),
            // A skill check's colour kind, prefixed to a checked dialogue response: white = retryable,
            // red = one-shot. Match I2 TOOLTIP_TUTO_CHECK_WHITE_OPEN ("White Check") and
            // TOOLTIP_TUTO_CHECK_RED ("Red Check").
            D("CheckWhite", "white check"),
            D("CheckRed", "red check"),
            // Noun, label spoken before a check's list of bonuses and penalties.
            D("CheckModifiers", "modifiers"),
            // {0} = the player's level in the skill being checked, read after the odds.
            D("CheckSkillLevel", "skill level {0}"),
            // Fallbacks for the game's cost-option labels, each spoken before a money amount: what a
            // paid dialogue option costs (I2 TOOLTIP_COST "Cost") and the player's wallet
            // (I2 TOOLTIP_YOU_HAVE "You have"). Used only if those terms fail to resolve.
            D("CostWord", "cost"),
            D("CostYouHave", "you have"),
            // Connectives in a resolved check's roll breakdown, composed as: "<total>/<target>: rolled
            // <die> plus <die>, plus <level> <skill name>, minus <n> <modifier name>". "rolled"
            // introduces the two dice; plus/minus join each bonus or penalty.
            D("CheckRolled", "rolled"),
            D("CheckPlus", "plus"),
            D("CheckMinus", "minus"),
            // Full sentence, spoken when the mod blocks the main menu's Collage button ("Collage" is
            // the game's name for its screenshot mode).
            D("CollageInaccessible", "Collage is a screenshot mode and is not accessible."),
            // The options row choosing the alternate language the player can flip to in play (the row
            // sits under the game's LANGUAGE header with no label of its own).
            D("SecondaryLanguage", "secondary language"),
            // Type-ahead search in lists: spoken when Escape clears the typed search.
            D("SearchCleared", "search cleared"),
            // Spoken when the typed search matches nothing; {0} = the typed letters.
            D("SearchNoMatch", "{0}, no match"),

            // Inventory.
            // Labels for the screen's Tab-stops, spoken when Tab enters each section.
            // The worn-equipment section (the paper-doll figure); adjective "equipped".
            D("InventoryEquipmentLabel", "equipped"),
            // The item-category tab row.
            D("InventoryTabsLabel", "categories"),
            // The item grid of the selected category.
            D("InventoryItemsLabel", "items"),
            // The stat-bonuses column.
            D("InventoryStatsLabel", "stats"),
            // Labels for the fixed keychain and bullets slots (door keys the player carries; bullets
            // for a gun), each spoken before that slot's contents.
            D("InventoryKeys", "keys"),
            D("InventoryBullets", "bullets"),
            // An equipment slot with nothing worn in it, spoken after the slot's caption
            // ("gloves, empty"), whose gender varies: carry a head noun rather than agree.
            D("InventorySlotEmpty", "empty"),
            // An item category with nothing in it.
            D("InventoryNoItems", "no items"),
            // Appended to an item acquired since the player last looked (the game's glow dot), after
            // the item's own name, whose gender varies: carry a head noun rather than agree.
            D("InventoryFresh", "new"),
            // Fallback for the game's consumable-effects header (I2
            // TOOLTIP_HOVER_SUBSTANCE_USED_EFFECTS), spoken before what using the item does; used only
            // if the term fails to resolve.
            D("ItemUseEffects", "effects when used"),
            // Fallback for the game's pawn-price label (I2 INVENTORY_TOOLTIP_PAWN_FOR "Pawn for"),
            // spoken before the price the pawnshop pays; likewise term-preferred.
            D("ItemPawnFor", "pawn for"),
            // The pawnshop's wallet readout; {0} = a formatted money phrase from WorldMoney
            // ("2.50 réal").
            D("PawnshopMoney", "money {0}"),
            // An item's worth; {0} = a formatted money phrase from WorldMoney.
            D("ItemValue", "value {0}"),
            // A consumable's remaining uses; {0} = the count.
            D("ItemUses", "{0} use|{0} uses"),
            // A continuous slider's position; {0} = whole-number percent of its travel.
            D("Percent", "{0} percent"),
            // A duration slider's value; {0} = the number of milliseconds.
            D("Milliseconds", "{0} milliseconds"),
            // A stepped slider's position when no authored words fit it; {0} = current step number,
            // {1} = step count ("step 2 of 3").
            D("Step", "step {0} of {1}"),
            // Fallback equipment-slot captions (what the slot holds), used only when the game's own
            // "<slot>Tag" label is missing. Nouns; "neck" = neckwear, "held left/right" = the item
            // carried in that hand.
            D("SlotHat", "hat"),
            D("SlotJacket", "jacket"),
            D("SlotShirt", "shirt"),
            D("SlotPants", "pants"),
            D("SlotGlasses", "glasses"),
            D("SlotNeck", "neck"),
            D("SlotGloves", "gloves"),
            D("SlotShoes", "shoes"),
            D("SlotHeldLeft", "held left"),
            D("SlotHeldRight", "held right"),

            // World navigation: the cursor's spatial readout, spoken as "<bearing>, <distance>" with a
            // vertical tag appended when the point is on another level ("north, 3 meters, above").
            // The compass is a '|'-separated ORDERED LIST (not plural forms): the eight bearings
            // clockwise from north; all eight entries are required, in this order.
            D("WorldCompass", "north|northeast|east|southeast|south|southwest|west|northwest"),
            // A distance under one metre (never spoken as "0 meters").
            D("WorldDistanceZero", "less than a meter"),
            // {0} = whole metres.
            D("WorldDistanceMeters", "{0} meter|{0} meters"),
            // The cursor sits exactly on the character's own position.
            D("WorldHere", "here"),
            // Vertical tags: the cursor's point is on a level above / below the character.
            D("WorldAbove", "above"),
            D("WorldBelow", "below"),
            // The mod's four world sensing systems, named in the mod menu: the spoken position readout,
            // the wall-proximity tones, the on-cursor object cue, and the periodic sonar sweep.
            D("WorldSystemSpatial", "cursor position"),
            D("WorldSystemWallTones", "wall tones"),
            D("WorldSystemObjectCue", "cursor objects"),
            D("WorldSystemSonar", "sonar"),

            // The scanner's category names: plural nouns, since a category is a set ("exits, 3"). The
            // taxonomy is the mod's own grouping of nearby world things.
            D("WorldScanEverything", "everything"),
            // Characters that can be talked to.
            D("WorldScanNpcs", "people"),
            // Objects that can be examined or used (not containers or doors).
            D("WorldScanInteractables", "interactables"),
            // Openable, lootable objects.
            D("WorldScanContainers", "containers"),
            // The floating skill-sense bubbles the game scatters (its SenseOrb); "orb" is the mod's
            // word for them throughout.
            D("WorldScanOrbs", "orbs"),
            // Doors, stairs, and other ways out of the current area.
            D("WorldScanExits", "exits"),
            // The quick-nav groups' labels, spoken only when a group key finds nothing ("items, none").
            D("WorldScanPeopleGroup", "people and interactables"),
            D("WorldScanItemsGroup", "items"),
            // Landing line when switching scan category; {0} = the category name, {1} = the count.
            D("WorldScanCategoryCount", "{0}, {1}"),
            // The same with nothing in the category; {0} = the category name, whose gender varies
            // across the categories above, so the word for "none" must not agree with it.
            D("WorldScanCategoryEmpty", "{0}, none"),
            // Spoken when the act-on-scanned key fires before anything was scanned.
            D("WorldScanNothing", "nothing scanned"),

            // Generic type words for world things whose own name is an unreadable dev slug (see
            // EntityNaming). Singular nouns.
            D("WorldThingDoor", "door"),
            D("WorldThingGate", "gate"),
            D("WorldThingStairs", "stairs"),
            D("WorldThingElevator", "elevator"),
            D("WorldThingLadder", "ladder"),
            D("WorldThingBoat", "boat"),
            D("WorldThingExit", "exit"),
            // Authored names for specific dev-named doors the game names nowhere: a door into a
            // bathroom, a door connecting two hotel rooms, and a door that is permanently locked.
            D("WorldThingBathroomDoor", "bathroom door"),
            D("WorldThingConnectingDoor", "connecting door"),
            // Locked, not merely shut: StatusOpen is the spoken contrast, so a word that also reads
            // as "closed" tells the player nothing. Reuse the game's own I2 TOOLTIP_LOCKED word.
            D("WorldThingLockedDoor", "locked door"),
            // Authored destination name for the harbour secretary's office: the game's own area
            // label for that interior is just "Harbour", the same word the actual harbour speaks,
            // so a door into the office would be indistinguishable by ear from a harbour gate.
            // Fills the destination slot of WorldExitNamed ("secretary's office door"). Noun phrase.
            // The secretary is a MAN in the script (the game's dialogue calls him one), which the
            // English possessive hides: a gendered language builds this on a masculine noun.
            D("WorldPlaceSecretaryOffice", "secretary's office"),
            // The same for the dockworkers' union boss's office (Evrart's container): its game area
            // label is also just "Harbour". "Union" matches the game's own vocabulary (I2 term
            // Items/union_membership_card names the "Débardeurs' Union"). Noun phrase.
            D("WorldPlaceUnionOffice", "union office"),
            // The same for the enterable cargo container in the container yard (Union-container-int),
            // the third and last area the game labels "Harbour". "Cargo container" is the game's own
            // examine wording ("A cargo container stands before you. One of many in this yard."), and
            // that line is the wording to match: dialogue elsewhere calls the same object by a second
            // phrase, which a translation must not take instead. Its owner is a discovery, so no name
            // that reveals one. Noun phrase.
            D("WorldPlaceCargoContainer", "cargo container"),
            // An exit named for where it goes: {0} = the destination name, {1} = the portal type word
            // ("Whirling in Rags door", "floor 2 stairs"); order the two as the language wants.
            D("WorldExitNamed", "{0} {1}"),
            // A numbered building storey; {0} = the floor number.
            D("WorldFloorNumber", "floor {0}"),
            // A below-ground storey.
            D("WorldBasement", "basement"),
            D("WorldThingContainer", "container"),
            // A lettered navigation buoy floating off the coast; "A" is its letter designation.
            D("WorldBuoyA", "buoy A"),
            D("WorldThingPerson", "person"),
            // A floating skill-sense bubble (see WorldScanOrbs).
            D("WorldThingOrb", "orb"),
            // A thought-cabinet orb riding the character (a thought, obsession, or paralyzer): its title
            // is meta text that would also name the thought early, so the type word is all it speaks.
            D("WorldThingThoughtOrb", "thought orb"),
            // An orb named by its clue text; {0} = the clue ("crack orb", "halogen watermarks orb").
            D("WorldOrbNamed", "{0} orb"),
            // The last-resort type word for anything else.
            D("WorldThingObject", "object"),

            // The spoken words for the generic container types EntityNaming recognizes in dev-side
            // object names (the matching stays on the English names, which the game keeps in every
            // language; only the SPOKEN word translates). Two '|' forms: singular, plural. All nouns
            // naming a physical lootable object in the street. Each word must name the PROPS the token
            // actually matches, which an ambiguous English noun does not settle: name the token's real
            // objects (they carry a ContainerSource) rather than the reading the English first suggests.
            D("ContainerWord_box", "box|boxes"),
            D("ContainerWord_crate", "crate|crates"),
            // A drink can (the noun, not the verb): the props are cola cans among returnable deposit tare.
            D("ContainerWord_can", "can|cans"),
            D("ContainerWord_bottle", "bottle|bottles"),
            D("ContainerWord_barrel", "barrel|barrels"),
            // A large street garbage container.
            D("ContainerWord_dumpster", "dumpster|dumpsters"),
            D("ContainerWord_bucket", "bucket|buckets"),
            // A glass jar.
            D("ContainerWord_jar", "jar|jars"),
            // A coarse cloth sack.
            D("ContainerWord_sack", "sack|sacks"),
            D("ContainerWord_bag", "bag|bags"),
            // A flower pot: the plant kind, the only lootable pot the game has.
            D("ContainerWord_pot", "pot|pots"),
            // A drinking cup.
            D("ContainerWord_cup", "cup|cups"),
            // A storage chest (the box, not the body part).
            D("ContainerWord_chest", "chest|chests"),
            // A fuel or fluid canister.
            D("ContainerWord_canister", "canister|canisters"),
            // The dev token for a metal box; speak it as the two words your language uses for one.
            D("ContainerWord_metalbox", "metalbox|metalboxes"),
            // Two dev spellings of the same street trash can; give both the same value.
            D("ContainerWord_trashcan", "trash can|trash cans"),
            D("ContainerWord_trash_can", "trash can|trash cans"),
            D("ContainerWord_drawer", "drawer|drawers"),
            D("ContainerWord_locker", "locker|lockers"),
            // A storage cabinet (furniture).
            D("ContainerWord_cabinet", "cabinet|cabinets"),
            // A strongbox (the noun, not the adjective).
            D("ContainerWord_safe", "safe|safes"),
            D("ContainerWord_wallet", "wallet|wallets"),
            // A metal grating over an opening.
            D("ContainerWord_grate", "grate|grates"),
            // An air vent: the grating itself (the props are wall vents along the boardwalk), never a
            // language's word for the building's ventilation system.
            D("ContainerWord_vent", "vent|vents"),
            // Loose money lying in the world (coins/bills to pick up); uncountable, both forms alike.
            D("ContainerWord_money", "money|money"),

            // The location readout: {0} = the game's localized area name, {1} = the floor word above
            // ("Whirling in Rags floor 2"); order the two as the language wants.
            D("WorldLocation", "{0} {1}"),

            // The walk-then-interact verb's spoken feedback (the character walking somewhere on the
            // player's order).
            // Committing a walk toward a named target; {0} = the target's spoken name.
            D("WorldMovingTo", "moving to {0}"),
            // Committing a walk to a bare spot of ground.
            D("WorldMoving", "moving"),
            // The walk to a bare spot finished.
            D("WorldArrived", "arrived"),
            // The target cannot be walked to from here; {0} = the target's spoken name.
            D("WorldUnreachableNamed", "{0}, can't reach"),
            D("WorldUnreachable", "can't reach"),
            // The player cancelled a walk in progress.
            D("WorldStopped", "stopped"),
            // A committed walk stalled mid-path (something blocked it) and gave up short of its spot.
            D("WorldStoppedShort", "stopped short"),
            // A walk was refused because an unresolved thought orb pins the character in place; full
            // clause telling the player to trigger the orb to get free.
            D("WorldOrbHolds", "held by an orb, resolve it to move"),
            // A world action (a walk, a screen open, a hand item) was refused because the game is not
            // accepting world input yet (a scripted scene still animating after a dialogue's last line,
            // a camera move, a transition); it clears by itself when control returns.
            D("WorldNoControl", "cutscene playing"),
            // The loot panel closed, however it was closed.
            D("WorldContainerClosed", "container closed"),
            // A container or door that refuses to open; adjective. Fallback for the game's tooltip
            // (I2 TOOLTIP_LOCKED "Locked"), used only if the term fails to resolve.
            D("StatusLocked", "locked"),
            // A door standing open, spoken after its name (only the open state is spoken; closed is
            // assumed). The state exists only on doors, so a gendered language agrees with its own
            // word for "door".
            D("StatusOpen", "open"),
            // A person with new dialogue waiting (the game pulses Kim's portrait for this); spoken
            // after the person's name: "Kim Kitsuragi, has something to say". Only Kim has the
            // mechanic, so a gendered language may agree with him.
            D("StatusHasSomethingToSay", "has something to say"),

            // World status readouts (spoken on their read keys).
            // The wallet total. {0} = the whole réal figure, {1} = the two-digit centims fraction, so
            // the decimal mark and currency word are the translation's to place. The currency name
            // matches I2 REAL_CURRENCY ("réal").
            D("WorldMoney", "{0}.{1} réal"),
            // The two damage bars in one line: {0}/{4} = the game's localized bar names (Health,
            // Morale), {1}-{2}/{5}-{6} = current/maximum numbers, {3}/{7} = the HealCharges phrases.
            D("WorldHealth", "{0} {1}/{2}, {3}; {4} {5}/{6}, {7}"),
            // The leveling readout: {0} = the game's localized "Experience" word, {1}/{2} =
            // XP into the current level over the level's cost, {3} = the SkillPoints phrase.
            D("WorldExperience", "{0}: {1}/{2}, {3}"),
            // Fallback for the game's THC_TOOLTIP_EXP term, used only if it fails to resolve.
            D("WorldExperienceLabel", "experience"),
            // Healing items assigned and ready to use on a bar; {0} = the count.
            D("HealCharges", "{0} healing charge|{0} healing charges"),
            // The loading screen's gameplay tip, labelled for what it is; {0} = the game's own
            // localized tip sentence.
            D("LoadingTip", "tip, {0}"),
            // The heal-or-die prompt when a bar hits zero and the game pauses for a timed heal window:
            // {0} = the game's localized bar name (Health heals with the left arrow key, Morale with
            // the right). Two whole sentences rather than a composed direction word, so each translates
            // as a sentence. The two bar names can differ in gender, so "critical" must either be
            // invariant or be recast around a noun of its own ("at a critical level").
            D("CrisisHealLeft", "{0} critical, press left arrow to heal"),
            D("CrisisHealRight", "{0} critical, press right arrow to heal"),

            // Bookmarks: named world positions the player saves and walks back to (the Ctrl+B menu).
            // The menu row that creates a bookmark at the character's position; verb phrase.
            D("BookmarkAdd", "add bookmark"),
            // Column header of a bookmark row's walk column (activating it walks the character there);
            // verb.
            D("ActionWalk", "walk"),
            // Column header of a bookmark row's delete column; verb.
            D("ActionDelete", "delete"),
            // A new bookmark's suggested name, offered in the name prompt before the player types
            // their own; {0} = a counter ("bookmark 3").
            D("BookmarkDefaultName", "bookmark {0}"),
            // A bookmark was created; {0} = its name.
            D("BookmarkSaved", "bookmark {0} saved"),
            // A bookmark was removed; {0} = its name.
            D("BookmarkDeleted", "bookmark {0} deleted"),
            // The bookmarks menu was opened with no game running (bookmarks live on a map).
            D("BookmarksUnavailable", "bookmarks need a loaded game"),
            // An add or delete could not write the bookmarks file (the log has the details).
            D("BookmarkWriteFailed", "bookmark file write failed"),

            // Cutscene descriptions: authored narration for the game's silent visual set pieces, written
            // to be unambiguous by ear (no words that collapse into a different meaning through a
            // synthesizer). Translate as flowing prose with the same restraint: present tense, plain
            // words, no added drama. This one narrates the new-game opening: the protagonist waking up
            // hungover on his trashed hostel room's floor.
            D("CutsceneNewGameWakeUp",
                "A room slowly fades in, seen from above. It is wrecked. A bed with rumpled white sheets sits "
                + "against the left wall. On the right, dark wallpaper patterned with hexagons, a dresser, and a "
                + "mirror. Papers, clothes and bottles are strewn across the tile floor. In the middle of the room "
                + "a man lies flat on the floor, face down, wearing nothing but his underwear and one sock. The "
                + "light swells like morning flooding in, then settles. The man stirs. He rolls over, pushes up "
                + "onto his hands and knees, and hangs there a moment, head down. Then he hauls himself to his "
                + "feet and stands swaying in the middle of the room."),
        };

        private static readonly Dictionary<string, string> English = BuildEnglish();

        private static Dictionary<string, string> BuildEnglish()
        {
            var map = new Dictionary<string, string>(Defaults.Length, System.StringComparer.Ordinal);
            foreach (var entry in Defaults) map[entry.Key] = entry.Value;
            return map;
        }

        /// <summary>Whether the table defines this key (what a translation may override).</summary>
        public static bool DefinesKey(string key) => English.ContainsKey(key);

        /// <summary>The translator-facing English template: a header explaining the format, the plural
        /// rule line, then every key with its English value, in table order.</summary>
        public static string DumpTemplate()
        {
            var sb = new StringBuilder();
            sb.Append("# Non-Visual Calculus authored strings - English template.\n");
            sb.Append("# To translate: copy to <language>.txt in this folder, named by the game's I2\n");
            sb.Append("# language code or lowercased language name (fr.txt or french.txt), and translate\n");
            sb.Append("# each value. Missing keys fall back to English, so a partial file works.\n");
            sb.Append("# {0}-style slots are filled at runtime; keep each one, but place it freely.\n");
            sb.Append("# '|' separates plural forms (or the compass list), chosen by the _plural rule:\n");
            sb.Append("# one, english, french, slavic, or arabic.\n");
            sb.Append(Translation.PluralKey).Append(" = english\n");
            foreach (var entry in Defaults)
                sb.Append(entry.Key).Append(" = ").Append(entry.Value).Append('\n');
            return sb.ToString();
        }

        // ---- lookup plumbing ----

        /// <summary>The live value for a key: the loaded translation's, else the English default.</summary>
        private static string T(string key)
        {
            if (!English.TryGetValue(key, out string english))
                throw new KeyNotFoundException("Strings table has no default for key '" + key + "'");
            return Translation.Get(key, english);
        }

        /// <summary>Format a template, un-RTL-fixing every string argument first: a game-text value can
        /// arrive display-shaped (visual order), and it must invert within its own slot before the
        /// authored template composes around it (see <see cref="Text.SpokenLine"/> for why the composed
        /// line is too late).</summary>
        private static string F(string key, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
                if (args[i] is string s) args[i] = Text.RtlText.Unfix(s);
            return string.Format(CultureInfo.InvariantCulture, T(key), args);
        }

        /// <summary>A counted phrase: the value's '|'-separated plural forms, picked by the loaded
        /// plural rule (the English rule for an untranslated key), then formatted with the count. A
        /// translation with fewer forms than its rule selects clamps to the last (the "other" form).</summary>
        private static string P(string key, int count)
        {
            string[] forms = T(key).Split('|');
            int index = Translation.Overrides(key) ? Translation.PluralIndex(count) : PluralRules.English(count);
            if (index >= forms.Length) index = forms.Length - 1;
            return string.Format(CultureInfo.InvariantCulture, forms[index], count);
        }

        /// <summary>One entry of a '|'-separated list value (the compass). A translation listing too few
        /// entries falls back to the English entry: for list semantics a clamped neighbour would speak
        /// WRONG information (a wrong bearing), and English-but-right beats translated-but-wrong.</summary>
        private static string Form(string key, int index)
        {
            string[] forms = T(key).Split('|');
            if (index < forms.Length) return forms[index];
            string[] english = English[key].Split('|');
            return index < english.Length ? english[index] : "";
        }

        /// <summary>The launch announcement; version is the mod's build version.</summary>
        public static string ModLoaded(string version) => F("ModLoaded", version);
        public static string ModuleFailed => T("ModuleFailed");
        /// <summary>Spoken once when a newer mod release exists; version is the new release's.</summary>
        public static string UpdateAvailable(string version) => F("UpdateAvailable", version);
        /// <summary>Spoken once at launch when the old Whirling in Words plugin is loaded beside this one.</summary>
        public static string LegacyModStillInstalled => T("LegacyModStillInstalled");

        // Input action descriptions (see the table above).
        public static string InputNavigateUp => T("InputNavigateUp");
        public static string InputNavigateDown => T("InputNavigateDown");
        public static string InputNavigateLeft => T("InputNavigateLeft");
        public static string InputNavigateRight => T("InputNavigateRight");
        public static string InputNextControl => T("InputNextControl");
        public static string InputPrevControl => T("InputPrevControl");
        public static string InputActivate => T("InputActivate");
        public static string InputBack => T("InputBack");
        public static string InputJumpFirst => T("InputJumpFirst");
        public static string InputJumpLast => T("InputJumpLast");
        public static string InputSecondary => T("InputSecondary");
        public static string InputScreenPrev => T("InputScreenPrev");
        public static string InputScreenNext => T("InputScreenNext");
        public static string InputModMenu => T("InputModMenu");
        public static string InputBookmarks => T("InputBookmarks");
        public static string InputWorldMoveNorth => T("InputWorldMoveNorth");
        public static string InputWorldMoveSouth => T("InputWorldMoveSouth");
        public static string InputWorldMoveEast => T("InputWorldMoveEast");
        public static string InputWorldMoveWest => T("InputWorldMoveWest");
        public static string InputWorldRecenter => T("InputWorldRecenter");
        public static string InputWorldInteract => T("InputWorldInteract");
        public static string InputWorldWalk => T("InputWorldWalk");
        public static string InputWorldStop => T("InputWorldStop");
        public static string InputWorldScanNext => T("InputWorldScanNext");
        public static string InputWorldScanPrev => T("InputWorldScanPrev");
        public static string InputWorldScanNextCategory => T("InputWorldScanNextCategory");
        public static string InputWorldScanPrevCategory => T("InputWorldScanPrevCategory");
        public static string InputWorldScanInteract => T("InputWorldScanInteract");
        public static string InputWorldScanPeopleNext => T("InputWorldScanPeopleNext");
        public static string InputWorldScanPeoplePrev => T("InputWorldScanPeoplePrev");
        public static string InputWorldScanItemsNext => T("InputWorldScanItemsNext");
        public static string InputWorldScanItemsPrev => T("InputWorldScanItemsPrev");
        public static string InputWorldScanExitsNext => T("InputWorldScanExitsNext");
        public static string InputWorldScanExitsPrev => T("InputWorldScanExitsPrev");
        public static string InputWorldScanCursor => T("InputWorldScanCursor");
        public static string InputWorldScanWaypoint => T("InputWorldScanWaypoint");
        public static string InputWorldInventory => T("InputWorldInventory");
        public static string InputWorldCharacterSheet => T("InputWorldCharacterSheet");
        public static string InputWorldJournal => T("InputWorldJournal");
        public static string InputWorldThoughtCabinet => T("InputWorldThoughtCabinet");
        public static string InputWorldMap => T("InputWorldMap");
        public static string InputWorldPause => T("InputWorldPause");
        public static string InputWorldHelp => T("InputWorldHelp");
        public static string InputWorldHealHealth => T("InputWorldHealHealth");
        public static string InputWorldHealMorale => T("InputWorldHealMorale");
        public static string InputWorldLeftHandItem => T("InputWorldLeftHandItem");
        public static string InputWorldRightHandItem => T("InputWorldRightHandItem");
        public static string InputWorldQuickSave => T("InputWorldQuickSave");
        public static string InputWorldQuickLoad => T("InputWorldQuickLoad");
        public static string InputWorldLanguage => T("InputWorldLanguage");
        public static string InputWorldReadTime => T("InputWorldReadTime");
        public static string InputWorldReadMoney => T("InputWorldReadMoney");
        public static string InputWorldReadHealth => T("InputWorldReadHealth");
        public static string InputWorldReadLocation => T("InputWorldReadLocation");
        public static string InputWorldReadExperience => T("InputWorldReadExperience");
        public static string InputKeyHelp => T("InputKeyHelp");
        public static string InputDialogueNumberJump => T("InputDialogueNumberJump");

        // The key-help screen's lines (see KeyHelp for the composition).
        /// <summary>One key-help line: an action's description and its key or keys.</summary>
        public static string KeyHelpLine(string action, string keys) => F("KeyHelpLine", action, keys);
        public static string KeyControl => T("KeyControl");
        public static string KeyShift => T("KeyShift");
        public static string KeyAlt => T("KeyAlt");
        public static string KeyUpArrow => T("KeyUpArrow");
        public static string KeyDownArrow => T("KeyDownArrow");
        public static string KeyLeftArrow => T("KeyLeftArrow");
        public static string KeyRightArrow => T("KeyRightArrow");
        public static string KeyReturn => T("KeyReturn");
        public static string KeyKeypadEnter => T("KeyKeypadEnter");
        public static string KeyEscape => T("KeyEscape");
        public static string KeyBackspace => T("KeyBackspace");
        public static string KeyTab => T("KeyTab");
        public static string KeyHome => T("KeyHome");
        public static string KeyEnd => T("KeyEnd");
        public static string KeySpace => T("KeySpace");
        public static string KeyComma => T("KeyComma");
        public static string KeyPeriod => T("KeyPeriod");
        public static string KeySlash => T("KeySlash");
        public static string KeyPageUp => T("KeyPageUp");
        public static string KeyPageDown => T("KeyPageDown");
        public static string KeyHelpGroupNavigate => T("KeyHelpGroupNavigate");
        public static string KeyHelpKeysArrows => T("KeyHelpKeysArrows");
        public static string KeyHelpGroupMoveCursor => T("KeyHelpGroupMoveCursor");
        public static string KeyHelpKeysWasd => T("KeyHelpKeysWasd");
        public static string KeyHelpKeysDigits => T("KeyHelpKeysDigits");
        public static string KeyHelpGroupNextPrevControl => T("KeyHelpGroupNextPrevControl");
        public static string KeyHelpGroupJumpFirstLast => T("KeyHelpGroupJumpFirstLast");
        public static string KeyHelpGroupScanThing => T("KeyHelpGroupScanThing");
        public static string KeyHelpGroupScanCategory => T("KeyHelpGroupScanCategory");
        public static string KeyHelpGroupScanPeople => T("KeyHelpGroupScanPeople");
        public static string KeyHelpGroupScanItems => T("KeyHelpGroupScanItems");
        public static string KeyHelpGroupScanExits => T("KeyHelpGroupScanExits");

        // Control role words, spoken after a control's label so the user knows what it is.
        public static string RoleButton => T("RoleButton");
        public static string RoleTab => T("RoleTab");

        // Marks the tab whose content is currently shown - the mod standard status word.
        public static string StatusSelected => T("StatusSelected");

        // Marks the save menu's create-new slot, so activating it reads as a new save, not an overwrite.
        public static string StatusNewSave => T("StatusNewSave");

        // The save menu's rename column (authored: editing is triggered by clicking the entry, so the
        // action has no game button to read a caption from). Activating it enters edit mode.
        public static string ActionRename => T("ActionRename");
        public static string StatusEditMode => T("StatusEditMode");

        // Options screen: control-type words, spoken after the setting name.
        public static string ControlSlider => T("ControlSlider");
        public static string ControlToggle => T("ControlToggle");
        public static string ControlDropdown => T("ControlDropdown");

        // Toggle status words.
        public static string StatusOn => T("StatusOn");
        public static string StatusOff => T("StatusOff");

        // Marks the signature skill (DE shows it only as a portrait emblem, no word to read).
        public static string StatusSignature => T("StatusSignature");

        // Marks a skill a point can be spent on right now (DE shows only a portrait glow).
        public static string StatusCanRaise => T("StatusCanRaise");

        /// <summary>The character sheet's pool of unspent skill points ("2 skill points"). DE shows it
        /// only as a row of pips with no spoken label.</summary>
        public static string SkillPoints(int count) => P("SkillPoints", count);

        // Spoken on the Adjust Abilities screen when a raise is rejected because the shared pool is
        // empty. DE only greys out the plus button, with no message a blind player could hear.
        public static string AbilityNoPointsLeft => T("AbilityNoPointsLeft");

        // Stepped-slider step words (Menu Size, Dialogue Text Size; the game keeps no per-step label).
        public static string StepSmall => T("StepSmall");
        public static string StepMedium => T("StepMedium");
        public static string StepLarge => T("StepLarge");

        // Spoken when a slider or stepper is adjusted past its end, so the user hears that it stopped
        // rather than the same value read back twice.
        public static string StatusMinimum => T("StatusMinimum");
        public static string StatusMaximum => T("StatusMaximum");

        // Thought cabinet status words (the slot and thought states are game enums shown only as art).
        public static string ThoughtSlotEmpty => T("ThoughtSlotEmpty");
        public static string ThoughtSlotUnlockable => T("ThoughtSlotUnlockable");
        public static string ThoughtSlotLocked => T("ThoughtSlotLocked");
        public static string ThoughtAvailable => T("ThoughtAvailable");
        public static string ThoughtResearched => T("ThoughtResearched");
        public static string ThoughtForgotten => T("ThoughtForgotten");
        public static string ThoughtUnknown => T("ThoughtUnknown");

        // The thought cabinet's two tab-stops, spoken as their labels when Tab enters them.
        public static string ThoughtSlotGridLabel => T("ThoughtSlotGridLabel");
        public static string ThoughtListLabel => T("ThoughtListLabel");

        // Research time, in-game hours and minutes: a cooking thought reads how much is left, an
        // available one how long it will take. The duration is a phrase from <see cref="Duration"/>,
        // and the translation places it, so neither label hardcodes English word order.
        public static string ThoughtTimeRemaining(string duration) => F("ThoughtTimeRemaining", duration);
        public static string ThoughtResearchTime(string duration) => F("ThoughtResearchTime", duration);

        /// <summary>A cooking thought's research stage, e.g. "researching, 40 percent".</summary>
        public static string ThoughtResearching(int percent) => F("ThoughtResearching", percent);

        /// <summary>An in-game duration in hours and minutes, e.g. "2 hours 15 minutes", "45 minutes",
        /// "3 hours". Zero reads as "less than a minute" (a research time that rounds to nothing left).
        /// The two-part form joins through its own template, so the separator is the translation's.</summary>
        public static string Duration(int minutes)
        {
            if (minutes <= 0)
                return T("DurationZero");
            int h = minutes / 60, m = minutes % 60;
            if (h > 0 && m > 0)
                return F("DurationHoursMinutes", P("DurationHours", h), P("DurationMinutes", m));
            return h > 0 ? P("DurationHours", h) : P("DurationMinutes", m);
        }

        // Journal task and subtask status words (DE shows a resolved task only as struck-through text).
        public static string JournalStatusActive => T("JournalStatusActive");
        public static string JournalStatusDone => T("JournalStatusDone");
        public static string JournalStatusCancelled => T("JournalStatusCancelled");
        // Marks a time-limited task. DE shows it only as a clock icon.
        public static string JournalTimed => T("JournalTimed");

        // The detail panel's filed/resolved time labels (the game's own resolution line reads stale for
        // an active task, so the line is composed from the model).
        public static string JournalFiled => T("JournalFiled");
        public static string JournalCompleted => T("JournalCompleted");
        public static string JournalForfeited => T("JournalForfeited");

        // Found white checks (map tab) state; DE shows these as colour, with no spoken word.
        public static string JournalCheckAvailable => T("JournalCheckAvailable");
        public static string JournalCheckLocked => T("JournalCheckLocked");
        public static string JournalCheckSeen => T("JournalCheckSeen");

        // Quicktravel points on the map (names drawn into the map art), and their state.
        public static string JournalYouAreHere => T("JournalYouAreHere");
        public static string JournalVisited => T("JournalVisited");
        public static string JournalLocChurch => T("JournalLocChurch");
        public static string JournalLocFishingVillage => T("JournalLocFishingVillage");
        public static string JournalLocWaterfront => T("JournalLocWaterfront");

        // List labels for the journal's tab-stops, spoken when Tab enters them.
        public static string JournalTasksLabel => T("JournalTasksLabel");
        public static string JournalTaskInfoLabel => T("JournalTaskInfoLabel");
        public static string JournalFastTravelLabel => T("JournalFastTravelLabel");
        public static string JournalWhiteChecksLabel => T("JournalWhiteChecksLabel");
        public static string JournalOfficerProfileLabel => T("JournalOfficerProfileLabel");

        // Screen names, spoken when a screen opens; mapped from the live view enum in ScreenAdapter.
        public static string ScreenMap => T("ScreenMap");
        public static string ScreenInventory => T("ScreenInventory");
        public static string ScreenClothing => T("ScreenClothing");
        public static string ScreenThoughtCabinet => T("ScreenThoughtCabinet");
        public static string ScreenJournal => T("ScreenJournal");
        public static string ScreenCharacterSheet => T("ScreenCharacterSheet");
        public static string ScreenArchetypeSelection => T("ScreenArchetypeSelection");
        public static string ScreenAdjustAbilities => T("ScreenAdjustAbilities");
        public static string ScreenSignatureSkill => T("ScreenSignatureSkill");
        public static string ScreenOptions => T("ScreenOptions");
        public static string ScreenSave => T("ScreenSave");
        public static string ScreenLoad => T("ScreenLoad");
        public static string ScreenMainMenu => T("ScreenMainMenu");
        public static string ScreenPauseMenu => T("ScreenPauseMenu");
        public static string ScreenHelp => T("ScreenHelp");
        public static string ScreenThought => T("ScreenThought");
        public static string ScreenCollage => T("ScreenCollage");
        public static string ScreenContainer => T("ScreenContainer");
        public static string ScreenNewspaper => T("ScreenNewspaper");
        public static string ScreenPawnshop => T("ScreenPawnshop");
        public static string ScreenThoughtComplete => T("ScreenThoughtComplete");
        public static string ScreenBeginPrompt => T("ScreenBeginPrompt");
        public static string ScreenModMenu => T("ScreenModMenu");
        public static string ScreenBookmarks => T("ScreenBookmarks");
        public static string ScreenKeyHelp => T("ScreenKeyHelp");
        public static string ScreenLearnSounds => T("ScreenLearnSounds");

        // The learn-sounds menu's sections and cursor-cue entries (its sonar entries reuse the
        // scanner's category words, so the menu and the scanner call a category the same thing).
        public static string LearnSoundsCursorSection => T("LearnSoundsCursorSection");
        public static string LearnSoundsSonarSection => T("LearnSoundsSonarSection");
        public static string SoundCursorEnter => T("SoundCursorEnter");
        public static string SoundCursorExit => T("SoundCursorExit");
        public static string SoundCursorImpassable => T("SoundCursorImpassable");
        public static string SoundCursorFogEnter => T("SoundCursorFogEnter");
        public static string SoundCursorFogExit => T("SoundCursorFogExit");
        public static string SoundOpenDoor => T("SoundOpenDoor");

        // Mod settings labels (the mod's own options, no game string to read).
        public static string SettingAutoReadDialogue => T("SettingAutoReadDialogue");
        public static string SettingReadAmbientDialogue => T("SettingReadAmbientDialogue");
        public static string SettingWallToneVolume => T("SettingWallToneVolume");
        public static string SettingWallTonesContinuous => T("SettingWallTonesContinuous");
        public static string SettingSonarVolume => T("SettingSonarVolume");
        public static string SettingSonarContinuous => T("SettingSonarContinuous");
        public static string SettingSonarRest => T("SettingSonarRest");
        // The per-category sonar toggles, composed from the scanner's category words so the menu and
        // the scanner call a category the same thing.
        public static string SettingSonarNpcs => F("SettingSonarCategory", WorldScanNpcs);
        public static string SettingSonarInteractables => F("SettingSonarCategory", WorldScanInteractables);
        public static string SettingSonarContainers => F("SettingSonarCategory", WorldScanContainers);
        public static string SettingSonarOrbs => F("SettingSonarCategory", WorldScanOrbs);
        public static string SettingSonarExits => F("SettingSonarCategory", WorldScanExits);
        public static string SettingScannerFromCursor => T("SettingScannerFromCursor");
        public static string SettingRunToDestinations => T("SettingRunToDestinations");
        public static string SettingUnrestrictCursor => T("SettingUnrestrictCursor");
        public static string SettingCheckForUpdates => T("SettingCheckForUpdates");

        // The navigable affordance that advances a conversation when there are no response choices
        // (DE's own continue control is an image with no clean text label).
        public static string DialogueContinue => T("DialogueContinue");

        // Spoken after a dialogue response the player has already selected before (the game dims it).
        public static string DialogueAlreadyChosen => T("DialogueAlreadyChosen");

        /// <summary>Spoken when a response activation is refused because the game's button is not
        /// accepting clicks yet (menu fade-in, an animation holding it); a later press works.</summary>
        public static string DialogueNotReady => T("DialogueNotReady");

        // The endgame newspaper's article-paging arrows, image-only buttons with no caption term.
        public static string NewspaperNextArticle => T("NewspaperNextArticle");
        public static string NewspaperPreviousArticle => T("NewspaperPreviousArticle");

        // A response's inline skill-check breakdown: the check colour words (white = retryable, red =
        // one-shot) and the trailing modifier-list label; the skill, difficulty, and odds are game text.
        public static string CheckWhite => T("CheckWhite");
        public static string CheckRed => T("CheckRed");
        public static string CheckModifiers => T("CheckModifiers");

        /// <summary>The player's skill level on an unrolled check ("skill level 3"), read after the
        /// odds; every number is a raw input, the odds carry the combined arithmetic.</summary>
        public static string CheckSkillLevelOf(int value) => F("CheckSkillLevel", value);

        // Fallbacks for the game's cost-option tooltip terms (TOOLTIP_COST / TOOLTIP_YOU_HAVE), used
        // only when a term fails to resolve; the game's words are preferred so they localize.
        public static string CostWord => T("CostWord");
        public static string CostYouHave => T("CostYouHave");

        // A resolved check's silent roll line's connectives ("<total>/<target>: rolled <d1> plus <d2>,
        // plus <skill> <name>, minus <n> <modifier>"); the rest of the line is game data.
        public static string CheckRolled => T("CheckRolled");
        public static string CheckPlus => T("CheckPlus");
        public static string CheckMinus => T("CheckMinus");

        // Spoken when the player activates the main menu's Collage button (a visual screenshot mode
        // with no accessible path; our navigator blocks the open and says why).
        public static string CollageInaccessible => T("CollageInaccessible");

        // The language the player taps Q/L to switch to in play (drawn under the shared LANGUAGE
        // header with no label of its own).
        public static string SecondaryLanguage => T("SecondaryLanguage");

        // Type-ahead search: spoken when clearing a live search (Escape).
        public static string SearchCleared => T("SearchCleared");

        /// <summary>Spoken when the typed search buffer matches no item in the focused list; the buffer
        /// text leads (the distinguishing part).</summary>
        public static string SearchNoMatch(string buffer) => F("SearchNoMatch", buffer);

        // ---- Inventory ----

        // The mod's labels for the equipment-doll slots, the section/list names, and the slot/tab
        // status words (fallbacks and section names DE does not expose).
        public static string InventoryEquipmentLabel => T("InventoryEquipmentLabel");
        public static string InventoryTabsLabel => T("InventoryTabsLabel");
        public static string InventoryItemsLabel => T("InventoryItemsLabel");
        public static string InventoryStatsLabel => T("InventoryStatsLabel");
        public static string InventoryKeys => T("InventoryKeys");
        public static string InventoryBullets => T("InventoryBullets");
        public static string InventorySlotEmpty => T("InventorySlotEmpty");
        public static string InventoryNoItems => T("InventoryNoItems");
        public static string InventoryFresh => T("InventoryFresh");

        // Fallback for the game's TOOLTIP_HOVER_SUBSTANCE_USED_EFFECTS header, used only when the term
        // fails to resolve; the game's word is preferred.
        public static string ItemUseEffects => T("ItemUseEffects");

        // Fallback for the game's INVENTORY_TOOLTIP_PAWN_FOR label ("Pawn for"), likewise.
        public static string ItemPawnFor => T("ItemPawnFor");

        /// <summary>The pawnshop's wallet readout: the player's current money, labelled for what it is.</summary>
        public static string PawnshopMoney(int centims) => F("PawnshopMoney", WorldMoney(centims));

        /// <summary>Fallback equipment-slot caption, keyed off the dock name, used only when the game's
        /// own "&lt;slot&gt;Tag" label is missing.</summary>
        public static string EquipmentSlotName(string dockName)
        {
            switch (dockName)
            {
                case "hat": return T("SlotHat");
                case "jacket": return T("SlotJacket");
                case "shirt": return T("SlotShirt");
                case "pants": return T("SlotPants");
                case "glasses": return T("SlotGlasses");
                case "neck": return T("SlotNeck");
                case "gloves": return T("SlotGloves");
                case "shoes": return T("SlotShoes");
                case "heldLeft": return T("SlotHeldLeft");
                case "heldRight": return T("SlotHeldRight");
                default: return dockName;
            }
        }

        /// <summary>An item's pawn value, spoken in the pawnables tab. The game stores it in centims and
        /// displays réal (its "0.50" for a stored 50), so the spoken figure matches the screen.</summary>
        public static string ItemValue(int centims) => F("ItemValue", WorldMoney(centims));

        /// <summary>A consumable's remaining uses.</summary>
        public static string ItemUses(int uses) => P("ItemUses", uses);

        /// <summary>A continuous slider's position as a percentage of its travel.</summary>
        public static string Percent(int value) => F("Percent", value);

        /// <summary>A duration slider's value in milliseconds.</summary>
        public static string Milliseconds(int value) => F("Milliseconds", value);

        /// <summary>A stepped slider's position when no authored words map to it.</summary>
        public static string Step(int index, int count) => F("Step", index, count);

        // ---- World navigation ----

        /// <summary>An eight-point compass word for a bearing index 0..7 (0 = north), or empty when out
        /// of range (the coincident "here" case, which the readout handles separately).</summary>
        public static string WorldCompass(int index)
            => index >= 0 && index < 8 ? Form("WorldCompass", index) : "";

        /// <summary>A whole-metre distance, e.g. "3 meters", "1 meter"; under a metre reads "less than a
        /// meter" so a near-but-not-coincident point never reads "0 meters". (Disco's 1 unit = 1 metre.)</summary>
        public static string WorldDistance(int meters)
            => meters <= 0 ? T("WorldDistanceZero") : P("WorldDistanceMeters", meters);

        public static string WorldHere => T("WorldHere");
        public static string WorldAbove => T("WorldAbove");
        public static string WorldBelow => T("WorldBelow");

        // The world sensing systems' names, spoken in the settings menu.
        public static string WorldSystemSpatial => T("WorldSystemSpatial");
        public static string WorldSystemWallTones => T("WorldSystemWallTones");
        public static string WorldSystemObjectCue => T("WorldSystemObjectCue");
        public static string WorldSystemSonar => T("WorldSystemSonar");

        // The scanner's spoken category names (the taxonomy is the mod's own grouping). Plural, since a
        // category is a set ("exits, 3").
        public static string WorldScanEverything => T("WorldScanEverything");
        public static string WorldScanNpcs => T("WorldScanNpcs");
        public static string WorldScanInteractables => T("WorldScanInteractables");
        public static string WorldScanContainers => T("WorldScanContainers");
        public static string WorldScanOrbs => T("WorldScanOrbs");
        public static string WorldScanExits => T("WorldScanExits");
        // The quick-nav groups' labels, spoken only when a group key finds nothing ("items, none").
        public static string WorldScanPeopleGroup => T("WorldScanPeopleGroup");
        public static string WorldScanItemsGroup => T("WorldScanItemsGroup");

        /// <summary>A scan category's landing line: its label and how many things are in it ("exits, 3";
        /// "orbs, none"). Label first, the distinguishing part.</summary>
        public static string WorldScanCategoryCount(string label, int count)
            => count == 0 ? F("WorldScanCategoryEmpty", label) : F("WorldScanCategoryCount", label, count);

        /// <summary>Spoken when the act-on-scanned key fires with nothing scanned yet.</summary>
        public static string WorldScanNothing => T("WorldScanNothing");

        // Generic type words: the spoken name for a thing whose own name is a slug and has no
        // spoiler-safe title to fall back to (see EntityNaming), so nothing the cursor passes over goes
        // nameless.
        public static string WorldThingDoor => T("WorldThingDoor");
        public static string WorldThingGate => T("WorldThingGate");
        public static string WorldThingStairs => T("WorldThingStairs");
        public static string WorldThingElevator => T("WorldThingElevator");
        public static string WorldThingLadder => T("WorldThingLadder");
        public static string WorldThingBoat => T("WorldThingBoat");
        public static string WorldThingExit => T("WorldThingExit");
        // Authored names for specific dev-named doors the game gives no examine actor and no readable
        // object name (see EntityNaming's door fallback table).
        public static string WorldThingBathroomDoor => T("WorldThingBathroomDoor");
        public static string WorldThingConnectingDoor => T("WorldThingConnectingDoor");
        public static string WorldThingLockedDoor => T("WorldThingLockedDoor");

        /// <summary>Authored destination name for the secretary's office interior, whose game area
        /// label ("Harbour") would hide which door enters it (see EntityNaming.AuthoredAreaName).</summary>
        public static string WorldPlaceSecretaryOffice => T("WorldPlaceSecretaryOffice");

        /// <summary>Authored destination name for the union boss's office, whose game area label
        /// is also "Harbour" (see EntityNaming.AuthoredAreaName).</summary>
        public static string WorldPlaceUnionOffice => T("WorldPlaceUnionOffice");

        /// <summary>Authored destination name for the container yard's enterable cargo container,
        /// the third area labeled "Harbour" (see EntityNaming.AuthoredAreaName).</summary>
        public static string WorldPlaceCargoContainer => T("WorldPlaceCargoContainer");

        /// <summary>An exit named for where it goes: the destination (or outdoor spot) and the portal
        /// type word, composed by the translation so the word order is its choice ("Whirling in Rags
        /// door", "balcony door", "floor 2 stairs").</summary>
        public static string ExitNamed(string destination, string typeWord)
            => F("WorldExitNamed", destination, typeWord);

        /// <summary>A numbered floor ("floor 2"), for an exit or location whose destination level is
        /// read from a scene id suffix.</summary>
        public static string FloorNumber(string number) => F("WorldFloorNumber", number);

        public static string WorldBasement => T("WorldBasement");
        public static string WorldThingContainer => T("WorldThingContainer");
        // The container token "buoya" is the internal spelling of the numbered coastal buoy "buoy A";
        // spoken so the letter reads as itself, not "buoya".
        public static string WorldBuoyA => T("WorldBuoyA");
        public static string WorldThingPerson => T("WorldThingPerson");
        public static string WorldThingOrb => T("WorldThingOrb");
        public static string WorldThingThoughtOrb => T("WorldThingThoughtOrb");
        public static string WorldThingObject => T("WorldThingObject");

        /// <summary>An orb named by its clue ("crack orb"): the clue and the type word, composed by the
        /// translation.</summary>
        public static string OrbNamed(string clue) => F("WorldOrbNamed", clue);

        /// <summary>The spoken word for a generic container type EntityNaming matched in a dev-side
        /// object name: the token's table entry (singular or plural form), or the token itself for a
        /// matcher word without an entry, so a new matcher word is never silent, just untranslated.</summary>
        public static string ContainerWord(string token, bool plural)
        {
            string key = "ContainerWord_" + token.Replace(' ', '_');
            if (!English.ContainsKey(key)) return token;
            string[] forms = T(key).Split('|');
            return forms[plural && forms.Length > 1 ? 1 : 0];
        }

        /// <summary>The location readout ('r'): the localized map name, plus the floor word for a
        /// numbered interior level ("Whirling in Rags floor 2"). The map name is the game's own
        /// localized area name.</summary>
        public static string WorldLocation(string map, string? floor)
            => string.IsNullOrEmpty(floor) ? map : F("WorldLocation", map, floor!);

        // The walk-then-interact verb's spoken feedback (DE has no equivalent lines).

        /// <summary>Spoken on committing the walk-then-interact verb toward a named target.</summary>
        public static string WorldMovingTo(string name)
            => string.IsNullOrEmpty(name) ? WorldMoving : F("WorldMovingTo", name);

        /// <summary>Spoken on moving to a bare-ground spot with no target.</summary>
        public static string WorldMoving => T("WorldMoving");

        /// <summary>Spoken on arriving at a bare-ground spot (a targeted move ends in its interaction,
        /// whose own readers speak instead).</summary>
        public static string WorldArrived => T("WorldArrived");

        /// <summary>Spoken when the target cannot be pathed to from where the character currently stands.</summary>
        public static string WorldUnreachable(string? name)
            => string.IsNullOrEmpty(name) ? T("WorldUnreachable") : F("WorldUnreachableNamed", name!);

        /// <summary>Spoken when the player cancels a committed walk.</summary>
        public static string WorldStopped => T("WorldStopped");

        /// <summary>Spoken when a committed walk stalls mid-path and gives up short of its spot, so a
        /// "moving" is never left dangling in silence.</summary>
        public static string WorldStoppedShort => T("WorldStoppedShort");

        /// <summary>Spoken when a walk is refused because a paralyzer or unresolved thought orb holds the
        /// character in place (the game's own movement block); the orb rides the character and must be
        /// triggered to release it.</summary>
        public static string WorldOrbHolds => T("WorldOrbHolds");

        /// <summary>Spoken when a world action (a walk, a screen open, a hand item) is refused because the
        /// game is not accepting world input (its input-lock gate is held by a scripted scene, a camera
        /// move, or a transition).</summary>
        public static string WorldNoControl => T("WorldNoControl");

        // ---- Bookmarks (the mod's own named world positions; DE has no equivalent) ----

        public static string BookmarkAdd => T("BookmarkAdd");
        public static string ActionWalk => T("ActionWalk");
        public static string ActionDelete => T("ActionDelete");

        /// <summary>A new bookmark's suggested name ("bookmark 3"), offered before the player types.</summary>
        public static string BookmarkDefaultName(int counter) => F("BookmarkDefaultName", counter);

        /// <summary>Spoken when a bookmark is created.</summary>
        public static string BookmarkSaved(string name) => F("BookmarkSaved", name);

        /// <summary>Spoken when a bookmark is deleted.</summary>
        public static string BookmarkDeleted(string name) => F("BookmarkDeleted", name);

        /// <summary>Spoken when the bookmarks menu is opened with no game loaded.</summary>
        public static string BookmarksUnavailable => T("BookmarksUnavailable");

        /// <summary>Spoken when an add or delete could not write the bookmarks file.</summary>
        public static string BookmarkWriteFailed => T("BookmarkWriteFailed");

        // ---- The world's loot panel (see the module's ContainerPanelScreen / ContainerReader) ----

        /// <summary>Spoken when the loot panel closes, whatever closed it - the panel is silent otherwise
        /// and a blind player must hear that the list they were arrowing through is gone.</summary>
        public static string WorldContainerClosed => T("WorldContainerClosed");

        /// <summary>Spoken when interacting with a locked container or door the game refuses to open (it
        /// plays only a rattle sound). Fallback for when the game's own "Locked" tooltip term fails to
        /// resolve; the term is preferred so the word localizes.</summary>
        public static string StatusLocked => T("StatusLocked");

        /// <summary>A door standing open, appended to its scanner readout. Closed is the default a blind
        /// player assumes, so only open is spoken. The game marks the state only by the door mesh's
        /// rotation, with no string to reuse.</summary>
        public static string StatusOpen => T("StatusOpen");

        /// <summary>A person with new dialogue waiting, appended to their cursor/scanner readout. The
        /// game shows this state only by pulsing Kim's HUD portrait (an animator, no string to
        /// reuse).</summary>
        public static string StatusHasSomethingToSay => T("StatusHasSomethingToSay");

        // ---- World status readouts (mod-authored; the game has no spoken equivalent) ----

        /// <summary>The wallet total. The game stores money in centims (100 = one réal) and its on-screen
        /// formatter prefixes a réal glyph the reader cannot speak, so the readout is composed from the
        /// raw value; the template places the decimal mark and currency word.</summary>
        public static string WorldMoney(int centims)
            => F("WorldMoney", centims / 100, (centims % 100).ToString("D2", CultureInfo.InvariantCulture));

        /// <summary>The two health bars (the game's own Health and Morale, not the Endurance/Volition
        /// skills that set their maximums), each current of maximum, plus the count of assigned healing
        /// charges. The bar names are passed in from the game so they localize.</summary>
        public static string WorldHealth(string healthName, int healthCurrent, int healthMax, int healthCharges,
                                         string moraleName, int moraleCurrent, int moraleMax, int moraleCharges)
            => F("WorldHealth", healthName, healthCurrent, healthMax, HealCharges(healthCharges),
                 moraleName, moraleCurrent, moraleMax, HealCharges(moraleCharges));

        /// <summary>A count of assigned healing charges, singular/plural.</summary>
        public static string HealCharges(int count) => P("HealCharges", count);

        /// <summary>The leveling readout: the game's "Experience" word, XP into the current level over
        /// the level's cost, and the pool of unspent skill points. The experience word is passed in from
        /// the game so it localizes; the numbers come from the live player-character model.</summary>
        public static string WorldExperience(string experienceLabel, int current, int cost, int skillPoints)
            => F("WorldExperience", experienceLabel, current, cost, SkillPoints(skillPoints));

        /// <summary>Authored fallback for the game's Experience term, used only if it fails to resolve.</summary>
        public static string WorldExperienceLabel => T("WorldExperienceLabel");

        // ---- Cutscene descriptions: authored narration for the game's silent visual set pieces,
        // spoken when the scene starts (see the table entry). ----
        public static string CutsceneNewGameWakeUp => T("CutsceneNewGameWakeUp");

        /// <summary>The loading screen's gameplay tip, labelled for what it is (the tip itself is the
        /// game's own localized string; the game has no header string for it).</summary>
        public static string LoadingTip(string text) => F("LoadingTip", text);

        /// <summary>The existential-crisis heal prompt: a bar (Health or Morale) hit zero and the game
        /// paused for a heal-or-die window, so this is the one notification spoken with interrupt. The
        /// bar name and gameMessage are the game's own localized strings; the heal-key hint is authored,
        /// because the heal window is the unusual, timed control that justifies a key hint (Health heals
        /// with Left, Morale with Right, matching the heal keys).</summary>
        public static string CrisisHeal(string barName, bool healWithLeft, string? gameMessage)
        {
            string prompt = F(healWithLeft ? "CrisisHealLeft" : "CrisisHealRight", barName);
            return string.IsNullOrEmpty(gameMessage) ? prompt : prompt + ". " + Text.RtlText.Unfix(gameMessage!);
        }
    }
}
