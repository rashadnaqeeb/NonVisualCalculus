using System.Collections.Generic;
using WhirlingInWords.Core.Input;
using WhirlingInWords.Core.Modularity;
using WhirlingInWords.Core.Strings;
using WhirlingInWords.Core.UI.Nav;
using WhirlingInWords.Core.Updates;
using WhirlingInWords.Module.Input;
using WhirlingInWords.Module.Nav;
using WhirlingInWords.Module.World;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Pad = InControl.InputControlType;

namespace WhirlingInWords.Module
{
    /// <summary>
    /// The reloadable UI reader, driven each frame by the host pump. It runs our own keyboard navigation
    /// via <see cref="ScreenManager"/>: on any screen with a registered
    /// <see cref="WhirlingInWords.Module.Nav.Screen"/> it takes the keyboard (mutes the game's input), builds
    /// the screen's tree, and drives the navigator from our own input. A save-name field temporarily gates
    /// our dispatch so typed keys reach it (see <see cref="TextEditGate"/>).
    ///
    /// This is the implementor the host loads by interface scan; future dialogue/inventory/world readers
    /// and any Harmony patches join it here.
    /// </summary>
    public sealed class UiModule : IModModule, IDevDriver
    {
        private IModHost _host;
        private Harmony _harmony;
        // The keyboard input substrate, owned here so it is rebuilt fresh on each hot-reload (a Core
        // static registry would accumulate duplicate registrations). Holds no native handle.
        private InputManager _input;
        // Our own UI navigation: it takes the keyboard on any screen with a registered Screen and drives
        // the navigator from our own input.
        private ScreenManager _screens;
        // The world-layer reader: owns the sensing overlay and drives it while the player is in the
        // isometric scene. Independent of the screen navigator (which handles menus).
        private WorldReader _world;
        // The world hotkeys that act on the game (open screens, status reads, quick-actions), as opposed to
        // the cursor verbs the reader handles.
        private WorldCommands _commands;
        // Speaks the game's own HUD notifications (money, health/morale changes, items, the timed crisis,
        // ...) via Harmony feeders drained from the pump. Owns no native handle; its patches ride _harmony.
        private NotificationReader _notifications;
        // Speaks the world's background barks (TV, ambient NPC chatter, proximity remarks) via a Harmony feeder
        // drained from the pump, gated on the world being active and the player's setting. Owns no native
        // handle; its patch rides _harmony.
        private BarkReader _barks;
        // Speaks the container events the game marks with sound alone (a locked refusal, the loot panel
        // closing) via Harmony feeders drained from the pump. Owns no native handle; its patches ride _harmony.
        private ContainerReader _containers;
        // Speaks the locked-door refusal, likewise marked with sound alone, via a Harmony feeder drained
        // from the pump. Owns no native handle; its patch rides _harmony.
        private DoorReader _doors;
        // Speaks the literary quote the game shows as a baked full-screen image (new game start and the
        // intro) via Harmony feeders drained from the pump. Owns no native handle; its patches ride _harmony.
        private QuoteReader _quotes;
        // Speaks the gameplay tip on the loading screen via a Harmony feeder drained from the pump.
        // Owns no native handle; its patch rides _harmony.
        private LoadingTipReader _loadingTips;
        // Speaks authored descriptions of the game's silent cinematic scenes (the new-game wake-up) via a
        // Harmony feeder drained from the pump. Owns no native handle; its patch rides _harmony.
        private CutsceneReader _cutscenes;
        // Keeps the mod's authored strings in the game's language (loads a lang/<language>.txt into
        // Core's Translation table, English when none). Owns no native handle.
        private LanguageSync _language;
        // Heals the game's stranded dialogue input lock (a vanilla wedge that permanently disables world
        // clicks) by firing the game's own unused recovery. Owns no native handle.
        private SequenceLockHealer _lockHealer;
        private static readonly InputCategory[] UiCategory = { InputCategory.UI };
        // Status precedes UI ON PURPOSE: in a screen that wants the status keys (dialogue) the heal arrows
        // (Status, Left/Right) shadow the inert UI Left/Right; the rest of UI (Up/Down/Tab/Enter/Escape/
        // Home/End/Backspace) binds no key Status binds, so it is unaffected.
        private static readonly InputCategory[] UiWithStatus = { InputCategory.Status, InputCategory.UI };
        private static readonly InputCategory[] WorldCategory = { InputCategory.World, InputCategory.Status };
        // The global mod-menu, bookmarks, and key-help hotkeys' action keys (internal ids, never spoken).
        private const string ModMenuAction = "mod.menu";
        private const string BookmarksAction = "mod.bookmarks";
        private const string KeyHelpAction = "mod.help.keys";
        // The bookmarks file (BepInEx config folder), read fresh by the bookmarks menu on each query.
        private BookmarkStore _bookmarks;
        // The single source of truth for "a game text field owns the keyboard" (grace-inclusive). While
        // Active, our navigator stands down so keystrokes reach the field; the input dispatcher set up in
        // Load gates on it, as must any future raw-key path (type-ahead). See TextEditGate for the why.
        private readonly TextEditGate _editGate = new TextEditGate();
        // Reads OS-typed characters into our navigator's type-ahead search each frame. Owns no native
        // handle (rebuilt fresh on reload); gates itself on the text-edit state below.
        private readonly TypeaheadInput _typeahead = new TypeaheadInput();
        // When this load happened, so the update announcement waits out the launch lines below.
        private float _loadedAt;
        // How long Tick holds a ready update-check result before speaking it: long enough for the
        // load announcement to be queued first, short enough to still land at the title screen.
        private const float UpdateAnnounceDelay = 3f;

        public void Load(IModHost host)
        {
            _host = host;
            // A per-load UNIQUE id so a reload's Dispose unpatches exactly this load's patches. The host
            // loads the new module (which patches) before disposing the old one (so a failed reload keeps
            // the old module running), and UnpatchSelf removes by owner id - with a fixed id the old
            // module's teardown stripped the fresh load's patches, silently killing every Harmony feature
            // (notifications, barks) until the next full restart.
            _harmony = new Harmony("com.rashad.whirlinginwords.module." + System.Guid.NewGuid().ToString("N"));

            // Match the mod's authored strings to the game language before anything captures or speaks
            // one (the input registrations below take their descriptions from the table).
            _language = new LanguageSync(_host);

            // Stand up the keyboard input substrate and our UI navigation.
            _input = new InputManager();
            _screens = new ScreenManager(_host);
            // The world sensing overlay, driven each frame while in the isometric scene.
            _world = new WorldReader(_host);
            _commands = new WorldCommands(_host);
            _lockHealer = new SequenceLockHealer(_host);
            _bookmarks = new BookmarkStore(_host);
            // Speak the game's HUD notifications; its Harmony patches register through this load's instance.
            _notifications = new NotificationReader(_host);
            _notifications.Apply(_harmony);
            // Speak the world's background barks; its Harmony patch registers through this load's instance.
            _barks = new BarkReader(_host);
            _barks.Apply(_harmony);
            // Speak the locked-container refusal and the loot panel's close; likewise on this load's Harmony.
            _containers = new ContainerReader(_host);
            _containers.Apply(_harmony);
            // Speak the locked-door refusal; likewise on this load's Harmony.
            _doors = new DoorReader(_host);
            _doors.Apply(_harmony);
            // Speak the baked-image quote when the game displays it; likewise on this load's Harmony.
            _quotes = new QuoteReader(_host);
            _quotes.Apply(_harmony);
            // Speak the loading screen's gameplay tip; likewise on this load's Harmony.
            _loadingTips = new LoadingTipReader(_host);
            _loadingTips.Apply(_harmony);
            // Speak the silent cinematic scenes' descriptions; likewise on this load's Harmony.
            _cutscenes = new CutsceneReader(_host);
            _cutscenes.Apply(_harmony);
            // Mute DE's raw-input number-key response-select while our navigator owns the keyboard, so a
            // digit only moves the dialogue cursor (jump-to-choice) instead of auto-committing the option.
            ButtonKeyTriggerGuard.Apply(_harmony, () => _screens.OwnsKeyboard);

            // UI navigation keys: live only while our navigator owns the keyboard, and routed into it by
            // the dispatcher below. Directions and Tab auto-repeat while held.
            _input.Register(UiActions.Up, Strings.InputNavigateUp, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow)).Repeating();
            _input.Register(UiActions.Down, Strings.InputNavigateDown, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow)).Repeating();
            _input.Register(UiActions.Left, Strings.InputNavigateLeft, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.LeftArrow)).Repeating();
            _input.Register(UiActions.Right, Strings.InputNavigateRight, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.RightArrow)).Repeating();
            _input.Register(UiActions.Next, Strings.InputNextControl, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab)).Repeating();
            _input.Register(UiActions.Prev, Strings.InputPrevControl, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab, shift: true)).Repeating();
            _input.Register(UiActions.Activate, Strings.InputActivate, InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Return)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter));
            _input.Register(UiActions.Back, Strings.InputBack, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Escape));
            // Backspace: a focused element's secondary/context action (e.g. an item's interact). It never
            // competes with type-ahead's delete: while a search buffer is live the navigator consumes this
            // action and the raw reader's '\b' does the deleting. Not repeating, so a held key does not
            // fire the context action repeatedly.
            _input.Register(UiActions.Secondary, Strings.InputSecondary, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace));
            _input.Register(UiActions.Home, Strings.InputJumpFirst, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            _input.Register(UiActions.End, Strings.InputJumpLast, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));

            // World keys: live only while the world reader owns the keyboard (free-roam, no menu taking it;
            // through an input-locked scripted tail the keyboard is held suspended and the keys that act on
            // the game refuse aloud - see WorldReader.Suspended). The WASD glide keys are polled as a held
            // vector each frame (read in Tick), not fired
            // handlers, so they carry no Performed callback and do not repeat; the verbs fire WorldReader
            // methods directly (like the mod-menu hotkey, never routed through the navigator).
            _input.Register(WorldActions.MoveNorth, Strings.InputWorldMoveNorth, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.W));
            _input.Register(WorldActions.MoveSouth, Strings.InputWorldMoveSouth, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.S));
            _input.Register(WorldActions.MoveEast, Strings.InputWorldMoveEast, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.D));
            _input.Register(WorldActions.MoveWest, Strings.InputWorldMoveWest, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.A));
            _input.Register(WorldActions.Recenter, Strings.InputWorldRecenter, InputCategory.World, () => _world.Recenter()).AddBinding(new KeyboardBinding(KeyCode.C));
            _input.Register(WorldActions.Interact, Strings.InputWorldInteract, InputCategory.World, () => _world.Interact())
                .AddBinding(new KeyboardBinding(KeyCode.Return)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter));
            // Backspace is free here: type-ahead's delete lives in the UI category, never the world's.
            _input.Register(WorldActions.Walk, Strings.InputWorldWalk, InputCategory.World, () => _world.Walk()).AddBinding(new KeyboardBinding(KeyCode.Backspace));
            _input.Register(WorldActions.Stop, Strings.InputWorldStop, InputCategory.World, () => _world.Cancel()).AddBinding(new KeyboardBinding(KeyCode.Space));

            // The scanner (review cursor): a second point of attention that never moves the cursor.
            // PageDown/PageUp cycle the current browse category nearest-first, Ctrl steps the category, the
            // punctuation row quick-navs a fixed group (comma people and interactables, period items, slash
            // exits; Shift reverses - the screen-reader quick-nav idiom), I walks-and-interacts with the
            // selection (bare I; Ctrl+I is the inventory, distinct by modifier - the same split as T time /
            // Ctrl+T thought cabinet), J moves the cursor to the selection's interaction point (bare J;
            // Ctrl+J is the journal), and P speaks the walking direction to the selection (the next leg of
            // the path the walk would take, where the readout's bearing is the straight line). Home and End
            // double for I and J (the UI Home/End actions are in the UI category, so the two never collide).
            _input.Register(WorldActions.ScanNext, Strings.InputWorldScanNext, InputCategory.World, () => _world.ScanNext()).AddBinding(new KeyboardBinding(KeyCode.PageDown));
            _input.Register(WorldActions.ScanPrev, Strings.InputWorldScanPrev, InputCategory.World, () => _world.ScanPrev()).AddBinding(new KeyboardBinding(KeyCode.PageUp));
            _input.Register(WorldActions.ScanNextCategory, Strings.InputWorldScanNextCategory, InputCategory.World, () => _world.ScanNextCategory()).AddBinding(new KeyboardBinding(KeyCode.PageDown, ctrl: true));
            _input.Register(WorldActions.ScanPrevCategory, Strings.InputWorldScanPrevCategory, InputCategory.World, () => _world.ScanPrevCategory()).AddBinding(new KeyboardBinding(KeyCode.PageUp, ctrl: true));
            _input.Register(WorldActions.ScanPeopleNext, Strings.InputWorldScanPeopleNext, InputCategory.World, () => _world.ScanPeople(1)).AddBinding(new KeyboardBinding(KeyCode.Comma));
            _input.Register(WorldActions.ScanPeoplePrev, Strings.InputWorldScanPeoplePrev, InputCategory.World, () => _world.ScanPeople(-1)).AddBinding(new KeyboardBinding(KeyCode.Comma, shift: true));
            _input.Register(WorldActions.ScanItemsNext, Strings.InputWorldScanItemsNext, InputCategory.World, () => _world.ScanItems(1)).AddBinding(new KeyboardBinding(KeyCode.Period));
            _input.Register(WorldActions.ScanItemsPrev, Strings.InputWorldScanItemsPrev, InputCategory.World, () => _world.ScanItems(-1)).AddBinding(new KeyboardBinding(KeyCode.Period, shift: true));
            _input.Register(WorldActions.ScanExitsNext, Strings.InputWorldScanExitsNext, InputCategory.World, () => _world.ScanExits(1)).AddBinding(new KeyboardBinding(KeyCode.Slash));
            _input.Register(WorldActions.ScanExitsPrev, Strings.InputWorldScanExitsPrev, InputCategory.World, () => _world.ScanExits(-1)).AddBinding(new KeyboardBinding(KeyCode.Slash, shift: true));
            _input.Register(WorldActions.ScanInteract, Strings.InputWorldScanInteract, InputCategory.World, () => _world.ScanInteract()).AddBinding(new KeyboardBinding(KeyCode.I)).AddBinding(new KeyboardBinding(KeyCode.Home));
            _input.Register(WorldActions.ScanCursor, Strings.InputWorldScanCursor, InputCategory.World, () => _world.ScanCursor()).AddBinding(new KeyboardBinding(KeyCode.J)).AddBinding(new KeyboardBinding(KeyCode.End));
            _input.Register(WorldActions.ScanWaypoint, Strings.InputWorldScanWaypoint, InputCategory.World, () => _world.ScanWaypoint()).AddBinding(new KeyboardBinding(KeyCode.P));

            // Information screens: the game's own hotkey letter under Ctrl, so the bare letters stay free for
            // the cursor/status keys (C recenters, T/M read time/money). They open the game's view; our screen
            // reader then drives it, and Escape (the screen's Back) closes it. The map has no standalone view
            // (it is a tab inside the journal, reachable via Ctrl+J), so it gets no key of its own.
            _input.Register(WorldActions.OpenInventory, Strings.InputWorldInventory, InputCategory.World, () => _commands.OpenInventory()).AddBinding(new KeyboardBinding(KeyCode.I, ctrl: true));
            _input.Register(WorldActions.OpenCharacterSheet, Strings.InputWorldCharacterSheet, InputCategory.World, () => _commands.OpenCharacterSheet()).AddBinding(new KeyboardBinding(KeyCode.C, ctrl: true));
            _input.Register(WorldActions.OpenJournal, Strings.InputWorldJournal, InputCategory.World, () => _commands.OpenJournal()).AddBinding(new KeyboardBinding(KeyCode.J, ctrl: true));
            _input.Register(WorldActions.OpenThoughtCabinet, Strings.InputWorldThoughtCabinet, InputCategory.World, () => _commands.OpenThoughtCabinet()).AddBinding(new KeyboardBinding(KeyCode.T, ctrl: true));
            _input.Register(WorldActions.Pause, Strings.InputWorldPause, InputCategory.World, () => _commands.Escape()).AddBinding(new KeyboardBinding(KeyCode.Escape));
            _input.Register(WorldActions.Help, Strings.InputWorldHelp, InputCategory.World, () => _commands.OpenHelp()).AddBinding(new KeyboardBinding(KeyCode.F1));

            // Gameplay quick-actions. Left/Right use the assigned heal item for the two bars (matching the
            // controller dpad); 1/2 use the hand-equipped items. The heal arrows are Status, not World, so
            // they stay live in a screen that wants them (the conversation view, where health can run out
            // mid-talk) without dragging the rest of the world keys in.
            _input.Register(WorldActions.HealEndurance, Strings.InputWorldHealHealth, InputCategory.Status, () => _commands.HealEndurance()).AddBinding(new KeyboardBinding(KeyCode.LeftArrow));
            _input.Register(WorldActions.HealVolition, Strings.InputWorldHealMorale, InputCategory.Status, () => _commands.HealVolition()).AddBinding(new KeyboardBinding(KeyCode.RightArrow));
            _input.Register(WorldActions.LeftHandItem, Strings.InputWorldLeftHandItem, InputCategory.World, () => _commands.UseLeftHand()).AddBinding(new KeyboardBinding(KeyCode.Alpha1));
            _input.Register(WorldActions.RightHandItem, Strings.InputWorldRightHandItem, InputCategory.World, () => _commands.UseRightHand()).AddBinding(new KeyboardBinding(KeyCode.Alpha2));

            // F5/F8 (and Alt+S/Alt+L) quicksave/quickload, global so they fire from a menu or a conversation
            // too, not just free-roam. Each hands the game its own action; the game's own CanSave/CanQuickLoad
            // gates decide silently, the mod neither gates nor announces. Not while a text field is editing.
            _input.Register(WorldActions.QuickSave, Strings.InputWorldQuickSave, InputCategory.Global,
                () => { if (!AnyTextEditActive) _commands.QuickSave(); })
                .AddBinding(new KeyboardBinding(KeyCode.F5))
                .AddBinding(new KeyboardBinding(KeyCode.S, alt: true));
            _input.Register(WorldActions.QuickLoad, Strings.InputWorldQuickLoad, InputCategory.Global,
                () => { if (!AnyTextEditActive) _commands.QuickLoad(); })
                .AddBinding(new KeyboardBinding(KeyCode.F8))
                .AddBinding(new KeyboardBinding(KeyCode.L, alt: true));

            // Status readouts: bare letters, each press re-reads (distinct by modifier from the Ctrl+letter
            // screen keys: Ctrl+T thought cabinet vs T time, etc.). Status, not World, so they stay live in a
            // screen that wants them (the conversation view).
            _input.Register(WorldActions.ReadTime, Strings.InputWorldReadTime, InputCategory.Status, () => _commands.ReadTime()).AddBinding(new KeyboardBinding(KeyCode.T));
            _input.Register(WorldActions.ReadMoney, Strings.InputWorldReadMoney, InputCategory.Status, () => _commands.ReadMoney()).AddBinding(new KeyboardBinding(KeyCode.M));
            _input.Register(WorldActions.ReadHealth, Strings.InputWorldReadHealth, InputCategory.Status, () => _commands.ReadHealth()).AddBinding(new KeyboardBinding(KeyCode.H));
            _input.Register(WorldActions.ReadLocation, Strings.InputWorldReadLocation, InputCategory.Status, () => _world.ReadLocation()).AddBinding(new KeyboardBinding(KeyCode.R));
            _input.Register(WorldActions.ReadExperience, Strings.InputWorldReadExperience, InputCategory.Status, () => _commands.ReadExperience()).AddBinding(new KeyboardBinding(KeyCode.X));

            // Ctrl+L is the game's language quick-switch (primary/secondary swap), global (the world and
            // menus), since the game's bare-key bindings are killed by type-ahead in our migrated screens.
            // Hands the game its own LanguageSwitch action; the game gates and switches. Not while editing.
            _input.Register(WorldActions.Language, Strings.InputWorldLanguage, InputCategory.Global,
                () => { if (!AnyTextEditActive) _commands.SwitchLanguage(); })
                .AddBinding(new KeyboardBinding(KeyCode.L, ctrl: true));

            // F12 opens/closes the mod's settings menu. Global, so it fires anywhere (the world, a game
            // menu, a conversation); the navigator then drives the overlay through the UI category above. Not
            // while any text edit owns the keyboard (a save-name field or a mod-owned bookmark-name edit),
            // so it never steals a keystroke and never yanks the overlay out from under a live edit.
            _input.Register(ModMenuAction, Strings.InputModMenu, InputCategory.Global,
                () => { if (!AnyTextEditActive) _screens.ToggleOverlay(new ModMenuScreen()); })
                .AddBinding(new KeyboardBinding(KeyCode.F12));

            // Ctrl+B opens/closes the bookmarks menu, the same overlay pattern as F12. Bookmarks live on
            // a map, so with no game loaded the toggle only says why nothing opened.
            _input.Register(BookmarksAction, Strings.InputBookmarks, InputCategory.Global,
                () => { if (!AnyTextEditActive) ToggleBookmarks(); })
                .AddBinding(new KeyboardBinding(KeyCode.B, ctrl: true));

            // Shift+F1 opens/closes the key-help screen, the same overlay pattern as F12 (bare F1 is the
            // game's own help overlay, distinct by modifier). Global, so it answers "what can I press"
            // in every context - the world, a menu, a dialogue.
            _input.Register(KeyHelpAction, Strings.InputKeyHelp, InputCategory.Global,
                () => { if (!AnyTextEditActive) ToggleKeyHelp(); })
                .AddBinding(new KeyboardBinding(KeyCode.F1, shift: true));

            // The game's own trigger cycle between the info screens (character sheet, inventory, journal,
            // thought cabinet), re-provided as pad-only UI actions: the game's handler decides where the
            // cycle applies, exactly like Escape. Falls through the navigator (not a nav key) to fire here.
            _input.Register(UiActions.ScreenPrev, Strings.InputScreenPrev, InputCategory.UI,
                () => { if (!AnyTextEditActive) _commands.CycleScreenPrev(); });
            _input.Register(UiActions.ScreenNext, Strings.InputScreenNext, InputCategory.UI,
                () => { if (!AnyTextEditActive) _commands.CycleScreenNext(); });

            // Controller bindings, layered onto the same actions as pad siblings of the keyboard combos
            // (the same lever that mutes the game's keyboard mutes its pad, and InControl keeps polling
            // devices for us - see GameInputMute and PadBinding). Each pad control mirrors one keyboard
            // key: the south face button is Enter (activate in menus, interact in the world), east is
            // Escape in menus and recenter in the world, north is Backspace (context action in
            // menus, walk in the world), west opens the character sheet, bumpers are Shift+Tab/Tab in
            // menus and the scanner step in the world, triggers cycle the game's info screens in menus
            // and are the J/I scan verbs in the world, stick clicks are the 1/2 hand items, Start
            // pauses, the dpad left/right follow the arrow keys (heals outside pure menus), dpad down
            // stops walking, and right-stick flicks speak the status readouts. The left stick doubles as
            // UI navigation in menus and the world glide vector in free-roam; the two categories are
            // never live together.
            AddPad(UiActions.Up, Pad.DPadUp, Pad.LeftStickUp);
            AddPad(UiActions.Down, Pad.DPadDown, Pad.LeftStickDown);
            AddPad(UiActions.Left, Pad.DPadLeft, Pad.LeftStickLeft);
            AddPad(UiActions.Right, Pad.DPadRight, Pad.LeftStickRight);
            AddPad(UiActions.Activate, Pad.Action1);
            AddPad(UiActions.Back, Pad.Action2);
            AddPad(UiActions.Secondary, Pad.Action4);
            AddPad(UiActions.Prev, Pad.LeftBumper);
            AddPad(UiActions.Next, Pad.RightBumper);
            AddPad(UiActions.ScreenPrev, Pad.LeftTrigger);
            AddPad(UiActions.ScreenNext, Pad.RightTrigger);

            AddPad(WorldActions.MoveNorth, Pad.LeftStickUp);
            AddPad(WorldActions.MoveSouth, Pad.LeftStickDown);
            AddPad(WorldActions.MoveEast, Pad.LeftStickRight);
            AddPad(WorldActions.MoveWest, Pad.LeftStickLeft);
            AddPad(WorldActions.Interact, Pad.Action1);
            AddPad(WorldActions.Recenter, Pad.Action2);
            AddPad(WorldActions.Walk, Pad.Action4);
            AddPad(WorldActions.OpenCharacterSheet, Pad.Action3);
            AddPad(WorldActions.Stop, Pad.DPadDown);
            AddPad(WorldActions.Pause, Pad.Command);
            AddPad(WorldActions.ScanPrev, Pad.LeftBumper);
            AddPad(WorldActions.ScanNext, Pad.RightBumper);
            AddPad(WorldActions.ScanCursor, Pad.LeftTrigger);
            AddPad(WorldActions.ScanInteract, Pad.RightTrigger);
            AddPad(WorldActions.LeftHandItem, Pad.LeftStickButton);
            AddPad(WorldActions.RightHandItem, Pad.RightStickButton);

            // The dpad follows the arrow keys: the two heals. Status category, so they shadow the inert
            // UI Left/Right in a dialogue, exactly like the keyboard heal arrows.
            AddPad(WorldActions.HealEndurance, Pad.DPadLeft);
            AddPad(WorldActions.HealVolition, Pad.DPadRight);

            // The right stick is the status compass: a flick speaks one readout (up health, right money,
            // down time, left experience). Status category, so the flicks stay live in dialogue like the
            // heals; a flick never collides with the stick's click, the right-hand item.
            AddPad(WorldActions.ReadHealth, Pad.RightStickUp);
            AddPad(WorldActions.ReadMoney, Pad.RightStickRight);
            AddPad(WorldActions.ReadTime, Pad.RightStickDown);
            AddPad(WorldActions.ReadExperience, Pad.RightStickLeft);

            // The live category each frame: the UI category while our navigator owns the keyboard (a
            // registered screen, no popup up), plus the Status keys when that screen wants them (the
            // conversation view); else World plus Status while the world reader owns the keyboard (free-roam,
            // suspended or not). A menu screen is authoritative, so UI wins when both could apply (a popup over
            // the world). Set after both managers resolve ownership in Tick, before input is polled.
            _input.ActiveCategoriesProvider = () =>
            {
                if (_screens.OwnsKeyboard && !_editGate.Active)
                    return _screens.StatusKeysActive ? UiWithStatus : UiCategory;
                if (_world.OwnsKeyboard) return WorldCategory;
                return null;
            };
            _input.JustPressedDispatcher = a =>
            {
                if (!_screens.OwnsKeyboard || _editGate.Active || a.Category != InputCategory.UI)
                    return false;
                return DispatchUiKey(a.Key);
            };

            // Surface any view ScreenAdapter neither names nor silences (e.g. one a game update added),
            // so it is noticed and named rather than going silently unannounced.
            foreach (var view in ScreenAdapter.UnmappedScreens())
                _host.LogWarning($"ScreenAdapter has no name or exclusion for view {view}; it will not be announced.");

            // Look up the mod's newest release in the background; Tick consumes the result. Core keeps
            // the once-per-process latch, so a hot-reload neither re-checks nor re-announces.
            if (_host.Settings.CheckForUpdates.Value)
                UpdateCheck.Start(_host.ModVersion, _host.LogInfo, _host.LogWarning);
            _loadedAt = Time.unscaledTime;
        }

        // Attach pad bindings to an already-registered action (the keyboard registrations above created
        // them all), so the whole controller map reads as one block.
        private void AddPad(string actionKey, params Pad[] controls)
        {
            foreach (InputAction a in _input.Actions)
                if (a.Key == actionKey)
                {
                    foreach (Pad c in controls) a.AddBinding(new PadBinding(c));
                    return;
                }
            _host.LogWarning($"UiModule: no action '{actionKey}' to bind a pad control to.");
        }

        // Whether any text edit owns the keyboard: a game field (grace-inclusive, see TextEditGate) or a
        // mod-owned edit (a bookmark name). The global hotkeys gate on this so they neither steal a
        // keystroke nor pull a menu out from under a live edit.
        private bool AnyTextEditActive => _editGate.Active || ModTextEntry.Active != null;

        // Toggle the bookmarks menu. Bookmarks hang off a map and the character's position, so with no
        // game loaded there is nothing to show - say why instead of opening an inert menu.
        private void ToggleBookmarks()
        {
            if (!_world.GameLoaded)
            {
                _host.Speech.Speak(Strings.BookmarksUnavailable, interrupt: true);
                return;
            }
            _screens.ToggleOverlay(new BookmarksScreen(_bookmarks, _world));
        }

        // Toggle the key-help screen. The lines are composed HERE, at the keypress, because opening the
        // overlay replaces the live key set with the overlay's own - this is the only moment the context
        // the player is asking about is readable. (On a toggle-close the compose is discarded unused.)
        // The direction fans and the next/previous pairs read as one line each; a group only collapses
        // while every member is live, so a context that claims one of the keys (the heal arrows in a
        // dialogue) lists the remaining members individually instead of promising all of them. The heal
        // arrows and the 1/2 hand items look like pairs but are NOT grouped: their two keys act on
        // different targets, and a collapsed line would lose which key does which.
        private void ToggleKeyHelp()
        {
            var groups = new[]
            {
                new KeyHelpGroup(Strings.KeyHelpGroupNavigate, Strings.KeyHelpKeysArrows,
                    new[] { UiActions.Up, UiActions.Down, UiActions.Left, UiActions.Right }),
                new KeyHelpGroup(Strings.KeyHelpGroupNextPrevControl, null,
                    new[] { UiActions.Next, UiActions.Prev }),
                new KeyHelpGroup(Strings.KeyHelpGroupJumpFirstLast, null,
                    new[] { UiActions.Home, UiActions.End }),
                new KeyHelpGroup(Strings.KeyHelpGroupMoveCursor, Strings.KeyHelpKeysWasd,
                    new[] { WorldActions.MoveNorth, WorldActions.MoveSouth, WorldActions.MoveEast, WorldActions.MoveWest }),
                new KeyHelpGroup(Strings.KeyHelpGroupScanThing, null,
                    new[] { WorldActions.ScanNext, WorldActions.ScanPrev }),
                new KeyHelpGroup(Strings.KeyHelpGroupScanCategory, null,
                    new[] { WorldActions.ScanNextCategory, WorldActions.ScanPrevCategory }),
                new KeyHelpGroup(Strings.KeyHelpGroupScanPeople, null,
                    new[] { WorldActions.ScanPeopleNext, WorldActions.ScanPeoplePrev }),
                new KeyHelpGroup(Strings.KeyHelpGroupScanItems, null,
                    new[] { WorldActions.ScanItemsNext, WorldActions.ScanItemsPrev }),
                new KeyHelpGroup(Strings.KeyHelpGroupScanExits, null,
                    new[] { WorldActions.ScanExitsNext, WorldActions.ScanExitsPrev }),
            };
            // The attached screen's own extra keys (the dialogue number jump) lead the list: they are the
            // most context-specific lines, and the registry's snapshot cannot know them.
            var lines = new List<string>(_screens.ScreenKeyHelpLines());
            lines.AddRange(KeyHelp.Compose(_input.SnapshotLiveKeys(), groups));
            _screens.ToggleOverlay(new KeyHelpScreen(lines));
        }

        // Route one UI action, shared by the live dispatcher and the dev seam so both honor the mod text
        // editor and the Escape handoff identically.
        private bool DispatchUiKey(string key)
        {
            // A mod-owned text edit owns the UI keys: Enter commits, Escape backs out, and the rest are
            // consumed so navigation stays frozen under the typing. Backspace (the Secondary action) is
            // consumed here but does its deleting through the typed '\b' the char reader feeds, the same
            // split as type-ahead's delete.
            IModTextSession edit = ModTextEntry.Active;
            if (edit != null)
            {
                if (key == UiActions.Activate) edit.Commit();
                else if (key == UiActions.Back) edit.Cancel();
                return true;
            }
            // Escape on a game view is handed to the game's own back action (context-sensitive: it closes
            // the open view or world container), so the game closes screens the way it does for a sighted
            // player - the mod reconstructs no close. Only the mod's own surfaces (a mod overlay, the
            // popup) handle Escape themselves, since the game has no equivalent to close.
            if (key == UiActions.Back && !_screens.OnOwnSurface)
            {
                _commands.Escape();
                return true;
            }
            return _screens.Dispatch(key);
        }

        public void Tick()
        {
            // Follow a game-language change (options menu or the Ctrl+L quick-switch) before anything speaks,
            // so this frame's announcements already use the right table.
            _language.Tick();

            // A rename cell entered edit mode last frame and parked its field here; focus it now, a frame
            // after the activating Enter, so the field does not consume that Enter and commit immediately.
            // Done before the editing check so the freshly focused field suppresses us this same frame.
            if (Nav.RenameCell.PendingActivation != null)
            {
                InputField pending = Nav.RenameCell.PendingActivation;
                Nav.RenameCell.PendingActivation = null;
                if (pending != null) { pending.Select(); pending.ActivateInputField(); }
            }

            // Recompute the text-edit gate up front, before input is polled: while a save-name field owns
            // the keyboard our navigator must stand down so keys reach it. A text edit does NOT hand the
            // keyboard back to the game (see TextEditGate); it only gates our own dispatch, via _editGate.
            _editGate.Update();

            // Heal a stranded dialogue input lock before ownership resolves, so the recovered control
            // (and the world reader's "map" announcement) lands this same frame.
            _lockHealer.Tick();

            // Resolve keyboard ownership for this frame BEFORE polling input (the live category gates on it):
            // our navigator takes the keyboard on a registered screen or the confirmation popup overlay. A
            // just-ended text edit asks the standing screen to re-read the focused control once.
            _screens.Tick(editEnded: _editGate.JustEnded);
            // Then the world reader resolves its own ownership: it yields to a menu screen (passed in) and
            // otherwise takes the keyboard in free-roam. Must run before input so the World category is live
            // when the glide keys are read below.
            _world.ResolveOwnership(_screens.OwnsKeyboard);

            // Poll our own keyboard input. A Global hotkey fires no matter what screen or popup is up; a
            // UI key routes into the navigator only while it owns the keyboard and is not gated for an edit;
            // a World verb (recenter, interact, stop) fires its WorldReader handler.
            _input.Tick(Time.unscaledTime);

            // Write any game-action press a world key just requested onto its InControl action, and release
            // the previous frame's, so the game's own handlers read it as a one-frame keypress (see there).
            Input.GameActionPress.Tick();

            // Feed OS-typed characters into a live mod-owned text edit (a bookmark name); its Enter and
            // Escape ride the bound UI actions through DispatchUiKey above.
            ModTextEntry.Tick();

            // Read OS-typed characters into the navigator's type-ahead search. Bound nav keys (arrows,
            // Home/End, Escape) drive the results through _input above; this reads only the unbound typed
            // text, gated on the text-edit state (game field or mod-owned edit) so a typed letter never
            // both names a bookmark and jumps a list at once.
            _typeahead.Tick(_screens, _editGate.Active || ModTextEntry.Active != null);

            // Speak "edit mode" as editing engages so the player knows they can type. The matching re-read
            // when editing ends is driven through _screens.Tick (editEnded) above, so it lands after any
            // save-list rebuild and as a single announce.
            if (_editGate.JustBegan)
                _host.Speech.Speak(Strings.StatusEditMode, interrupt: false);

            // Drive the world sensing overlay and cursor. It self-gates on the in-game (CLEAR) view, so it is
            // idle in menus, dialogue, and at the title. The held WASD vector (live only while the world owns
            // the keyboard) glides the cursor; the interact/recenter/stop verbs fired above act on it.
            float glideX = 0f, glideZ = 0f;
            if (_world.OwnsKeyboard)
            {
                if (_input.Held(WorldActions.MoveEast)) glideX += 1f;
                if (_input.Held(WorldActions.MoveWest)) glideX -= 1f;
                if (_input.Held(WorldActions.MoveNorth)) glideZ += 1f;
                if (_input.Held(WorldActions.MoveSouth)) glideZ -= 1f;
            }
            _world.Tick(glideX, glideZ);

            // Speak any HUD notifications the game raised since last frame (the crisis interrupts; the rest
            // queue). Drained here so they are announced from one place on the pump, like every other readout.
            _notifications.Drain();

            // Speak any world barks captured since last frame (queued, never interrupting). Self-gates on the
            // world being the live layer and the player's setting, and drops barks caught while it is not.
            _barks.Drain();

            // Speak any container cues (locked refusal, panel close), after the notifications so a take
            // reads "received ..." before "container closed".
            _containers.Drain();

            // Speak a locked-door refusal caught since last frame.
            _doors.Drain();

            // Speak the quote if the game displayed it since last frame (queued, so the begin-prompt
            // announcement it accompanies reads first).
            _quotes.Drain();

            // Speak the loading screen's tip if one came up since last frame (queued).
            _loadingTips.Drain();

            // Speak a cinematic scene's description if one started since last frame (queued, so the
            // dialogue line that triggered it finishes first).
            _cutscenes.Drain();

            // Say so if a newer mod release exists (staged by the launch check), held a beat so the
            // launch lines queue first. One-shot: TakeUpdate hands the result over once per process.
            // Up to date or a failed check stays silent (the latter is in the log).
            if (Time.unscaledTime - _loadedAt >= UpdateAnnounceDelay)
            {
                string newVersion = UpdateCheck.TakeUpdate();
                if (newVersion != null)
                    _host.Speech.Speak(Strings.UpdateAvailable(newVersion), interrupt: false);
            }
        }

        // Dev seam (IDevDriver): drive our navigator from the dev server's /input, the headless counterpart
        // to a real key. Mirrors the live JustPressedDispatcher: dispatch only while our navigator owns the
        // keyboard and no text field has it, and hand an unconsumed Escape back to the game. Returns null
        // when our navigator is not driving, so the host falls back to the game's own input injector.
        public string DispatchUi(string action)
        {
            if (_screens == null || !_screens.OwnsKeyboard || _editGate.Active)
                return null;
            return (DispatchUiKey(action) ? "consumed " : "unconsumed ") + action;
        }

        // Dev seam (IDevDriver): type text into a live mod-owned edit (a bookmark name), the headless
        // counterpart of the OS-typed characters ModTextEntry reads. Null when no mod edit is active, so
        // the dev server falls back to the game-field injector.
        public string TypeText(string text)
        {
            IModTextSession edit = ModTextEntry.Active;
            if (edit == null)
                return null;
            foreach (char c in text ?? "")
            {
                if (c == '\b') edit.Backspace();
                else if (!char.IsControl(c)) edit.Type(c);
            }
            return "typed into the mod text editor\n";
        }

        // Dev seam (IDevDriver): our navigator's live state for the dev server's /nav.
        public string DescribeNav() => _screens != null ? _screens.DescribeNav() : "(no screen manager)\n";

        // Dev seam (IDevDriver): fire a world verb through its registered handler, the headless
        // counterpart to a real world key. Gated exactly like the live keys: a Global action (quicksave,
        // the F12/Ctrl+B menu toggles) is live everywhere, so it fires no matter who owns the keyboard;
        // anything else only while the world reader owns it (null hands the caller a "not driving"
        // verdict rather than firing a world verb under a menu). Status-category actions (the readouts,
        // quick-heals) fire here too - they are live in the world category set.
        public string DriveWorld(string actionKey)
        {
            if (_input == null || _world == null || _editGate.Active)
                return null;
            if (!IsGlobalAction(actionKey) && !_world.OwnsKeyboard)
                return null;
            return _input.FireAction(actionKey)
                ? "fired " + actionKey
                : "unknown action " + actionKey;
        }

        private bool IsGlobalAction(string key)
        {
            foreach (InputAction a in _input.Actions)
                if (a.Key == key) return a.Category == InputCategory.Global;
            return false;
        }

        public void Dispose()
        {
            // Hand the keyboard back to the game before tearing down, so a reload never leaves the game's
            // action set muted or InControl frozen mid-injection.
            Input.GameActionPress.Reset();
            _screens.HandBack();
            _world?.Dispose(); // disengage the overlay (release any audio voices) before the context drops
            _notifications?.Dispose(); // drop the static back-reference before the patches are removed
            _barks?.Dispose(); // likewise drop the bark feeder's back-reference before unpatching
            _containers?.Dispose(); // and the container feeder's
            _doors?.Dispose(); // and the door feeder's
            _quotes?.Dispose(); // and the quote feeder's
            _loadingTips?.Dispose(); // and the tip feeder's
            _cutscenes?.Dispose(); // and the cutscene feeder's
            _harmony?.UnpatchSelf();
            _harmony = null;
            ModTextEntry.Active = null; // a live bookmark-name edit dies with its overlay
            _input = null; // owns no native handle; the registration list goes with the dropped context
            _screens = null;
            _world = null;
            _commands = null;
            _lockHealer = null;
            _bookmarks = null;
            _notifications = null;
            _barks = null;
            _containers = null;
            _doors = null;
            _quotes = null;
            _loadingTips = null;
            _cutscenes = null;
            // The Translation table is deliberately NOT reset here: on a hot-reload the new module has
            // already loaded its own table, and a reset from the old module's teardown would wipe it
            // (the same trap as a fixed Harmony id).
            _language = null;
            _host = null;
        }
    }
}
