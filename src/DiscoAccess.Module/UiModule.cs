using DiscoAccess.Core.Input;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using DiscoAccess.Module.Input;
using DiscoAccess.Module.Nav;
using DiscoAccess.Module.World;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DiscoAccess.Module
{
    /// <summary>
    /// The reloadable UI reader, driven each frame by the host pump. It runs our own keyboard navigation
    /// via <see cref="ScreenManager"/>: on any screen with a registered
    /// <see cref="DiscoAccess.Module.Nav.Screen"/> it takes the keyboard (mutes the game's input), builds
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
        private static readonly InputCategory[] UiCategory = { InputCategory.UI };
        // Status precedes UI ON PURPOSE: in a screen that wants the status keys (dialogue) the heal arrows
        // (Status, Left/Right) shadow the inert UI Left/Right; the rest of UI (Up/Down/Tab/Enter/Escape/
        // Home/End/Backslash) binds no key Status binds, so it is unaffected.
        private static readonly InputCategory[] UiWithStatus = { InputCategory.Status, InputCategory.UI };
        private static readonly InputCategory[] WorldCategory = { InputCategory.World, InputCategory.Status };
        // The global mod-menu hotkey's action key (internal id, never spoken).
        private const string ModMenuAction = "mod.menu";
        // The single source of truth for "a game text field owns the keyboard" (grace-inclusive). While
        // Active, our navigator stands down so keystrokes reach the field; the input dispatcher set up in
        // Load gates on it, as must any future raw-key path (type-ahead). See TextEditGate for the why.
        private readonly TextEditGate _editGate = new TextEditGate();
        // Reads OS-typed characters into our navigator's type-ahead search each frame. Owns no native
        // handle (rebuilt fresh on reload); gates itself on the text-edit state below.
        private readonly TypeaheadInput _typeahead = new TypeaheadInput();

        public void Load(IModHost host)
        {
            _host = host;
            // A per-load UNIQUE id so a reload's Dispose unpatches exactly this load's patches. The host
            // loads the new module (which patches) before disposing the old one (so a failed reload keeps
            // the old module running), and UnpatchSelf removes by owner id - with a fixed id the old
            // module's teardown stripped the fresh load's patches, silently killing every Harmony feature
            // (notifications, barks) until the next full restart.
            _harmony = new Harmony("com.rashad.discoaccess.module." + System.Guid.NewGuid().ToString("N"));

            // Stand up the keyboard input substrate and our UI navigation.
            _input = new InputManager();
            _screens = new ScreenManager(_host);
            // The world sensing overlay, driven each frame while in the isometric scene.
            _world = new WorldReader(_host);
            _commands = new WorldCommands(_host);
            // Speak the game's HUD notifications; its Harmony patches register through this load's instance.
            _notifications = new NotificationReader(_host);
            _notifications.Apply(_harmony);
            // Speak the world's background barks; its Harmony patch registers through this load's instance.
            _barks = new BarkReader(_host);
            _barks.Apply(_harmony);

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
            // Backslash: a focused element's secondary/context action (e.g. an item's interact). Kept off
            // Backspace so it never competes with type-ahead's delete. Not repeating, so a held key does not
            // fire the context action repeatedly.
            _input.Register(UiActions.Secondary, Strings.InputSecondary, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backslash));
            _input.Register(UiActions.Home, Strings.InputJumpFirst, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            _input.Register(UiActions.End, Strings.InputJumpLast, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));

            // World keys: live only while the world reader owns the keyboard (free-roam with control, no menu
            // taking it). The WASD glide keys are polled as a held vector each frame (read in Tick), not fired
            // handlers, so they carry no Performed callback and do not repeat; the verbs fire WorldReader
            // methods directly (like the mod-menu hotkey, never routed through the navigator).
            _input.Register(WorldActions.MoveNorth, Strings.InputWorldMoveNorth, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.W));
            _input.Register(WorldActions.MoveSouth, Strings.InputWorldMoveSouth, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.S));
            _input.Register(WorldActions.MoveEast, Strings.InputWorldMoveEast, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.D));
            _input.Register(WorldActions.MoveWest, Strings.InputWorldMoveWest, InputCategory.World).AddBinding(new KeyboardBinding(KeyCode.A));
            _input.Register(WorldActions.Recenter, Strings.InputWorldRecenter, InputCategory.World, () => _world.Recenter()).AddBinding(new KeyboardBinding(KeyCode.C));
            _input.Register(WorldActions.Interact, Strings.InputWorldInteract, InputCategory.World, () => _world.Interact())
                .AddBinding(new KeyboardBinding(KeyCode.Return)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter));
            _input.Register(WorldActions.Stop, Strings.InputWorldStop, InputCategory.World, () => _world.Cancel()).AddBinding(new KeyboardBinding(KeyCode.Space));

            // The scanner (review cursor), the WOTR key scheme: PageDown/PageUp cycle the reviewed thing
            // nearest-first, Ctrl steps the browse category, Home plants the movement cursor on it, and I
            // walks-and-interacts with it (bare I; Ctrl+I is the inventory, distinct by modifier - the same
            // split as T time / Ctrl+T thought cabinet).
            _input.Register(WorldActions.ScanNext, Strings.InputWorldScanNext, InputCategory.World, () => _world.ScanNext()).AddBinding(new KeyboardBinding(KeyCode.PageDown));
            _input.Register(WorldActions.ScanPrev, Strings.InputWorldScanPrev, InputCategory.World, () => _world.ScanPrev()).AddBinding(new KeyboardBinding(KeyCode.PageUp));
            _input.Register(WorldActions.ScanNextCategory, Strings.InputWorldScanNextCategory, InputCategory.World, () => _world.ScanNextCategory()).AddBinding(new KeyboardBinding(KeyCode.PageDown, ctrl: true));
            _input.Register(WorldActions.ScanPrevCategory, Strings.InputWorldScanPrevCategory, InputCategory.World, () => _world.ScanPrevCategory()).AddBinding(new KeyboardBinding(KeyCode.PageUp, ctrl: true));
            _input.Register(WorldActions.ScanCursorTo, Strings.InputWorldScanCursorTo, InputCategory.World, () => _world.ScanCursorTo()).AddBinding(new KeyboardBinding(KeyCode.Home));
            _input.Register(WorldActions.ScanInteract, Strings.InputWorldScanInteract, InputCategory.World, () => _world.ScanInteract()).AddBinding(new KeyboardBinding(KeyCode.I));

            // Information screens: the game's own hotkey letter under Ctrl, so the bare letters stay free for
            // the cursor/status keys (C recenters, T/M read time/money). They open the game's view; our screen
            // reader then drives it, and Escape (the screen's Back) closes it. The map has no standalone view
            // (it is a tab inside the journal, reachable via Ctrl+J), so it gets no key of its own.
            _input.Register(WorldActions.OpenInventory, Strings.InputWorldInventory, InputCategory.World, () => _commands.OpenInventory()).AddBinding(new KeyboardBinding(KeyCode.I, ctrl: true));
            _input.Register(WorldActions.OpenCharacterSheet, Strings.InputWorldCharacterSheet, InputCategory.World, () => _commands.OpenCharacterSheet()).AddBinding(new KeyboardBinding(KeyCode.C, ctrl: true));
            _input.Register(WorldActions.OpenJournal, Strings.InputWorldJournal, InputCategory.World, () => _commands.OpenJournal()).AddBinding(new KeyboardBinding(KeyCode.J, ctrl: true));
            _input.Register(WorldActions.OpenThoughtCabinet, Strings.InputWorldThoughtCabinet, InputCategory.World, () => _commands.OpenThoughtCabinet()).AddBinding(new KeyboardBinding(KeyCode.T, ctrl: true));
            _input.Register(WorldActions.Pause, Strings.InputWorldPause, InputCategory.World, () => _commands.OpenPauseMenu()).AddBinding(new KeyboardBinding(KeyCode.Escape));
            _input.Register(WorldActions.Help, Strings.InputWorldHelp, InputCategory.World, () => _commands.OpenHelp()).AddBinding(new KeyboardBinding(KeyCode.F1));

            // Gameplay quick-actions. Left/Right use the assigned heal item for the two bars (matching the
            // controller dpad); 1/2 use the hand-equipped items; F5/F8 quicksave/quickload. The heal arrows
            // are Status, not World, so they stay live in a screen that wants them (the conversation view,
            // where health can run out mid-talk) without dragging the rest of the world keys in.
            _input.Register(WorldActions.HealEndurance, Strings.InputWorldHealHealth, InputCategory.Status, () => _commands.HealEndurance()).AddBinding(new KeyboardBinding(KeyCode.LeftArrow));
            _input.Register(WorldActions.HealVolition, Strings.InputWorldHealMorale, InputCategory.Status, () => _commands.HealVolition()).AddBinding(new KeyboardBinding(KeyCode.RightArrow));
            _input.Register(WorldActions.LeftHandItem, Strings.InputWorldLeftHandItem, InputCategory.World, () => _commands.UseLeftHand()).AddBinding(new KeyboardBinding(KeyCode.Alpha1));
            _input.Register(WorldActions.RightHandItem, Strings.InputWorldRightHandItem, InputCategory.World, () => _commands.UseRightHand()).AddBinding(new KeyboardBinding(KeyCode.Alpha2));
            _input.Register(WorldActions.QuickSave, Strings.InputWorldQuickSave, InputCategory.World, () => _commands.QuickSave()).AddBinding(new KeyboardBinding(KeyCode.F5));
            _input.Register(WorldActions.QuickLoad, Strings.InputWorldQuickLoad, InputCategory.World, () => _commands.QuickLoad()).AddBinding(new KeyboardBinding(KeyCode.F8));

            // Status readouts: bare letters, each press re-reads (distinct by modifier from the Ctrl+letter
            // screen keys: Ctrl+T thought cabinet vs T time, etc.). Status, not World, so they stay live in a
            // screen that wants them (the conversation view).
            _input.Register(WorldActions.ReadTime, Strings.InputWorldReadTime, InputCategory.Status, () => _commands.ReadTime()).AddBinding(new KeyboardBinding(KeyCode.T));
            _input.Register(WorldActions.ReadMoney, Strings.InputWorldReadMoney, InputCategory.Status, () => _commands.ReadMoney()).AddBinding(new KeyboardBinding(KeyCode.M));
            _input.Register(WorldActions.ReadHealth, Strings.InputWorldReadHealth, InputCategory.Status, () => _commands.ReadHealth()).AddBinding(new KeyboardBinding(KeyCode.H));
            _input.Register(WorldActions.ReadLocation, Strings.InputWorldReadLocation, InputCategory.Status, () => _world.ReadLocation()).AddBinding(new KeyboardBinding(KeyCode.R));

            // Ctrl+L cycles the game language, global (the world and menus), since the game's bare-key binding
            // is killed by type-ahead in our migrated screens. Not while a text field is editing.
            _input.Register(WorldActions.Language, Strings.InputWorldLanguage, InputCategory.Global,
                () => { if (!_editGate.Active) _commands.CycleLanguage(); })
                .AddBinding(new KeyboardBinding(KeyCode.L, ctrl: true));

            // Ctrl+M opens/closes the mod's settings menu. Global, so it fires anywhere (the world, a game
            // menu, a conversation); the navigator then drives the overlay through the UI category above. Not
            // while a game text field owns the keyboard, so it never steals a keystroke from a save-name edit.
            _input.Register(ModMenuAction, Strings.InputModMenu, InputCategory.Global,
                () => { if (!_editGate.Active) _screens.ToggleModMenu(); })
                .AddBinding(new KeyboardBinding(KeyCode.M, ctrl: true));

            // The live category each frame: the UI category while our navigator owns the keyboard (a
            // registered screen, no popup up), plus the Status keys when that screen wants them (the
            // conversation view); else World plus Status while the world reader owns the keyboard (free-roam
            // with control). A menu screen is authoritative, so UI wins when both could apply (a popup over
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
                bool consumed = _screens.Dispatch(a.Key);
                // An Escape our navigator did not consume means the screen has no Back of its own (the title
                // menu): hand the keyboard back so the game's own Escape runs (nothing at the title, matching
                // vanilla; resume in the pause menu) rather than swallowing it.
                if (!consumed && a.Key == UiActions.Back)
                    _screens.DeferEscapeToGame();
                return consumed;
            };

            // Surface any view ScreenAdapter neither names nor silences (e.g. one a game update added),
            // so it is noticed and named rather than going silently unannounced.
            foreach (var view in ScreenAdapter.UnmappedScreens())
                _host.LogWarning($"ScreenAdapter has no name or exclusion for view {view}; it will not be announced.");
        }

        public void Tick()
        {
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

            // Read OS-typed characters into the navigator's type-ahead search. Bound nav keys (arrows,
            // Home/End, Escape) drive the results through _input above; this reads only the unbound typed
            // text, gated on the same text-edit state so it never fights the save-name field.
            _typeahead.Tick(_screens, _editGate.Active);

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
        }

        // Dev seam (IDevDriver): drive our navigator from the dev server's /input, the headless counterpart
        // to a real key. Mirrors the live JustPressedDispatcher: dispatch only while our navigator owns the
        // keyboard and no text field has it, and hand an unconsumed Escape back to the game. Returns null
        // when our navigator is not driving, so the host falls back to the game's own input injector.
        public string DispatchUi(string action)
        {
            if (_screens == null || !_screens.OwnsKeyboard || _editGate.Active)
                return null;
            bool consumed = _screens.Dispatch(action);
            if (!consumed && action == UiActions.Back)
            {
                _screens.DeferEscapeToGame();
                return "back handed to the game (screen has no Back of its own)";
            }
            return (consumed ? "consumed " : "unconsumed ") + action;
        }

        // Dev seam (IDevDriver): our navigator's live state for the dev server's /nav.
        public string DescribeNav() => _screens != null ? _screens.DescribeNav() : "(no screen manager)\n";

        public void Dispose()
        {
            // Hand the keyboard back to the game before tearing down, so a reload never leaves InControl
            // disabled.
            _screens.HandBack();
            _world?.Dispose(); // disengage the overlay (release any audio voices) before the context drops
            _notifications?.Dispose(); // drop the static back-reference before the patches are removed
            _barks?.Dispose(); // likewise drop the bark feeder's back-reference before unpatching
            _harmony?.UnpatchSelf();
            _harmony = null;
            _input = null; // owns no native handle; the registration list goes with the dropped context
            _screens = null;
            _world = null;
            _commands = null;
            _notifications = null;
            _barks = null;
            _host = null;
        }
    }
}
