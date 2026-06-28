using DiscoAccess.Core.Input;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI.Nav;
using DiscoAccess.Module.Input;
using DiscoAccess.Module.Nav;
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
        private static readonly InputCategory[] UiCategory = { InputCategory.UI };
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
            // A per-load id so a reload's Dispose unpatches exactly this load's patches. No patches yet;
            // future readers register them through this instance.
            _harmony = new Harmony("com.rashad.discoaccess.module");

            // Stand up the keyboard input substrate and our UI navigation.
            _input = new InputManager();
            _screens = new ScreenManager(_host);

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
            _input.Register(UiActions.Home, Strings.InputJumpFirst, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            _input.Register(UiActions.End, Strings.InputJumpLast, InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));

            // The UI category is live only while our navigator owns the keyboard (a registered screen, no
            // popup up); a fired UI key then routes into the navigator. Set by the ScreenManager's Tick,
            // which runs before input is polled.
            _input.ActiveCategoriesProvider = () => (_screens.OwnsKeyboard && !_editGate.Active) ? UiCategory : null;
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

            // Resolve keyboard ownership for this frame BEFORE polling input (the UI category gates on it):
            // our navigator takes the keyboard on a registered screen or the confirmation popup overlay. A
            // just-ended text edit asks the standing screen to re-read the focused control once.
            _screens.Tick(editEnded: _editGate.JustEnded);

            // Poll our own keyboard input. A Global hotkey fires no matter what screen or popup is up; a
            // UI key routes into the navigator only while it owns the keyboard and is not gated for an edit.
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
            _harmony?.UnpatchSelf();
            _harmony = null;
            _input = null; // owns no native handle; the registration list goes with the dropped context
            _screens = null;
            _host = null;
        }
    }
}
