using System;
using DiscoAccess.Core.Input;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.Strings;
using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;
using DiscoAccess.Module.Input;
using DiscoAccess.Module.Nav;
using HarmonyLib;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiscoAccess.Module
{
    /// <summary>
    /// The reloadable UI reader, driven each frame by the host pump. It runs two navigation paths and
    /// chooses per frame which owns the keyboard (see <see cref="Tick"/>):
    ///
    /// The default is our own keyboard navigation, via <see cref="ScreenManager"/>: on any screen with a
    /// registered <see cref="DiscoAccess.Module.Nav.Screen"/> it takes the keyboard (mutes the game's
    /// input), builds the screen's tree, and drives the navigator from our own input.
    ///
    /// The fallback, for screens not yet migrated to a registered Screen, follows the game's own focus:
    /// it polls DE's NavigationManager for the current uGUI selection and, on a change, announces it
    /// (navigation interrupts, per the house rule) - an options control as label, type, value, and
    /// tooltip via <see cref="OptionAdapter"/>; an Adjust Abilities stat via <see cref="AbilityAdapter"/>;
    /// a signature skill portrait via <see cref="SkillAdapter"/>; anything else via the generic
    /// <see cref="FocusReader"/> - and re-announces just the changed part on an in-place change. It also
    /// announces the screen the player just opened. The confirmation popup is driven by the ScreenManager
    /// overlay (it owns the keyboard while up), so this fallback stands down for it like any owned screen.
    ///
    /// This is the implementor the host loads by interface scan; future dialogue/inventory/world readers
    /// and any Harmony patches join it here, and the focus-follower shrinks as screens migrate.
    /// </summary>
    public sealed class FocusModule : IModModule, IDevDriver
    {
        private IModHost _host;
        private Harmony _harmony;
        // The keyboard input substrate, owned here so it is rebuilt fresh on each hot-reload (a Core
        // static registry would accumulate duplicate registrations). Holds no native handle.
        private InputManager _input;
        // Our own UI navigation, the default way menus are read. It takes the keyboard on any screen with
        // a registered Screen; the legacy focus-follower below is the fallback for not-yet-migrated
        // screens and is being superseded screen by screen.
        private ScreenManager _screens;
        private static readonly InputCategory[] UiCategory = { InputCategory.UI };
        private IntPtr _lastSelected = IntPtr.Zero;
        // The single source of truth for "a game text field owns the keyboard" (grace-inclusive). While
        // Active, our navigator stands down so keystrokes reach the field; the input dispatcher set up in
        // Load gates on it, as must any future raw-key path (type-ahead). See TextEditGate for the why.
        private readonly TextEditGate _editGate = new TextEditGate();
        // Reads OS-typed characters into our navigator's type-ahead search each frame. Owns no native
        // handle (rebuilt fresh on reload); gates itself on the text-edit state below.
        private readonly TypeaheadInput _typeahead = new TypeaheadInput();
        // The value-only readout of the focused options control, for detecting an in-place change
        // (adjusting a slider, toggling) where focus does not move. Null when the focus is not an
        // options control.
        private string _lastOptionValue;
        // The value-and-grade readout of the focused ability (Adjust Abilities screen), for detecting an
        // in-place change (pressing plus or minus) where focus does not move. Null when the focus is not
        // an ability control.
        private string _lastAbilityValue;
        // The signature marker of the focused skill (signature skill screen), for detecting the player
        // setting it as their signature where focus does not move. Null when the focus is not a skill.
        private string _lastSkillSignature;
        // The authored name of the screen last announced, for detecting a screen change. Null before any
        // named screen is shown. (The confirmation popup is driven by the ScreenManager overlay, not here.)
        private string _lastScreen;
        // When a screen opens its focus settles over several frames (e.g. options lands on the tab
        // header, then on the first setting), and only the last is worth speaking. While the unscaled
        // clock is below this deadline, focus announcements are skipped so those transients are not voiced
        // and do not interrupt the screen name; the control current when it expires is then spoken once.
        private float _screenSettleUntil = float.NegativeInfinity;
        // Set with the settle window so the control spoken when it expires queues behind the screen name
        // instead of interrupting it; cleared once that one focus is spoken.
        private bool _suppressFocusInterrupt;

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

            // Our navigator owns the keyboard this frame (driving a registered screen, possibly gated for a
            // text edit): it, or the edit, handled the frame; nothing for the focus-follower to do. Clear its
            // dedup state so a later hand-off to a not-yet-migrated screen re-announces the screen name and
            // landing control instead of treating frozen state as already spoken.
            if (_screens.OwnsKeyboard)
            {
                _lastScreen = null;
                _lastSelected = IntPtr.Zero;
                return;
            }

            // The view system is not up yet (early boot): the focus-follower below reads
            // ViewsPagesBridge/NavigationManager, which throw until it is, so stand down for now.
            if (!_screens.ViewReady)
                return;

            // Fallback for not-yet-migrated screens: follow the game's own focus. A newly opened screen is
            // announced here and opens a short settle window; the focus it reveals is then skipped until
            // the window expires and spoken once, queued behind the name.
            TickScreen();

            // Let the screen's focus settle before reading it (see _screenSettleUntil): skip the focus
            // work entirely, leaving _lastSelected unadvanced so the settled control reads when it ends.
            if (Time.unscaledTime < _screenSettleUntil)
                return;

            Selectable selected = CurrentSelectable();
            // Dedup on the native address. (A destroyed Selectable's address can in principle be
            // reused by a new one; in practice a menu rebuild passes through a null-selection frame,
            // which resets this, so that collision effectively can't slip through.)
            IntPtr ptr = selected != null ? selected.Pointer : IntPtr.Zero;

            if (ptr != _lastSelected)
            {
                if (selected == null)
                {
                    _lastSelected = ptr;
                    _lastOptionValue = null;
                    _lastAbilityValue = null;
                    _lastSkillSignature = null;
                    return;
                }

                // A just-opened screen has already interrupted with its name; this first focus queues
                // behind it. Otherwise navigation interrupts as usual.
                bool interrupt = !_suppressFocusInterrupt;
                _suppressFocusInterrupt = false;

                // The in-place re-read trackers below belong to whichever structured control is now
                // focused; clear them up front so a control that is none of these leaves all clear.
                _lastOptionValue = null;
                _lastAbilityValue = null;
                _lastSkillSignature = null;

                OptionState option = OptionAdapter.TryRead(selected, withTooltip: true);
                AbilityState ability = option == null ? AbilityAdapter.TryRead(selected) : null;
                SkillState skill = option == null && ability == null ? SkillAdapter.TryRead(selected) : null;
                if (option != null)
                {
                    _host.Speech.Speak(OptionAnnouncer.Compose(option), interrupt: interrupt);
                    _lastOptionValue = OptionAnnouncer.ComposeValue(option);
                }
                else if (ability != null)
                {
                    // An Adjust Abilities stat reads through its own structured adapter; the generic sweep
                    // would speak the pip diamonds, duplicate the value, and voice the plus/minus arrows.
                    _host.Speech.Speak(AbilityAnnouncer.Compose(ability), interrupt: interrupt);
                    _lastAbilityValue = AbilityAnnouncer.ComposeValue(ability);
                }
                else if (skill != null)
                {
                    // A signature-skill portrait reads through its own structured adapter; the generic
                    // sweep finds only the icon button and speaks nothing, and the on-screen description
                    // panel never follows controller focus.
                    _host.Speech.Speak(SkillAnnouncer.Compose(skill), interrupt: interrupt);
                    _lastSkillSignature = SkillAnnouncer.ComposeSignature(skill);
                }
                else
                {
                    // A save/load entry and an archetype button each read cleanly through their own
                    // structured adapter; the generic sweep would speak a save entry's uppercased name and
                    // "| " timestamp dividers, or an archetype's stacked flip-clock animation layers as
                    // duplicated digits. Other focus falls through to the generic reader.
                    string text = Compose(selected);
                    if (!string.IsNullOrEmpty(text))
                        _host.Speech.Speak(text, interrupt: interrupt);
                }

                // Advance only after a successful read/speak, so an exception in the speech path (caught
                // and logged by the host pump) leaves the change un-acknowledged and retried next frame
                // rather than permanently suppressed.
                _lastSelected = ptr;
                return;
            }

            // Same control still focused: re-announce just the value when it changed in place (a slider
            // adjusted, a toggle flipped, an ability raised or lowered) so the user hears the result of
            // their own input.
            if (selected != null && _lastOptionValue != null)
            {
                OptionState option = OptionAdapter.TryRead(selected, withTooltip: false);
                if (option != null)
                {
                    string value = OptionAnnouncer.ComposeValue(option);
                    if (value != _lastOptionValue)
                    {
                        _host.Speech.Speak(value, interrupt: true);
                        _lastOptionValue = value;
                    }
                }
            }
            else if (selected != null && _lastAbilityValue != null)
            {
                AbilityState ability = AbilityAdapter.TryRead(selected);
                if (ability != null)
                {
                    string value = AbilityAnnouncer.ComposeValue(ability);
                    if (value != _lastAbilityValue)
                    {
                        _host.Speech.Speak(value, interrupt: true);
                        _lastAbilityValue = value;
                    }
                }
            }
            else if (selected != null && _lastSkillSignature != null)
            {
                SkillState skill = SkillAdapter.TryRead(selected);
                if (skill != null)
                {
                    string signature = SkillAnnouncer.ComposeSignature(skill);
                    if (signature != _lastSkillSignature)
                    {
                        // Voice only the gain of the marker (setting this skill as signature); an empty
                        // marker still updates the tracker so it does not re-fire.
                        if (!string.IsNullOrEmpty(signature))
                            _host.Speech.Speak(signature, interrupt: true);
                        _lastSkillSignature = signature;
                    }
                }
            }
        }

        // Route a freshly focused control to its structured adapter, falling back to the generic label
        // sweep. Options are handled by the caller (their value is also polled for in-place changes).
        private static string Compose(Selectable selected)
        {
            SaveEntryState save = SaveEntryAdapter.TryRead(selected);
            if (save != null)
                return SaveEntryAnnouncer.Compose(save);

            ArchetypeState archetype = ArchetypeAdapter.TryRead(selected);
            if (archetype != null)
                return ArchetypeAnnouncer.Compose(archetype);

            return FocusReader.Read(selected);
        }

        // The window after a screen opens during which focus is left to settle before it is read. Long
        // enough to span the open's transient selections (the tab header before the first setting), short
        // enough to stay hidden behind the screen name's own speech, so the settled control adds no
        // perceived delay.
        private const float SettleSeconds = 0.3f;

        // Announce the screen the player just opened, speaking its authored name. The screen name
        // supersedes whatever was being said (the user pressed a button to get here), so it interrupts;
        // the control that lands must then queue behind it rather than clobber it. Opening a settle window
        // both skips the open's transient focuses and holds _suppressFocusInterrupt until the settled
        // control is spoken (the open sets no new selection the dedup would catch on its own, so
        // _lastSelected is reset to force that read).
        private void TickScreen()
        {
            string screen = ScreenAdapter.CurrentScreenName();
            if (screen == _lastScreen)
                return;

            _lastScreen = screen;
            if (string.IsNullOrEmpty(screen))
                return;

            _host.Speech.Speak(screen, interrupt: true);
            _lastSelected = IntPtr.Zero;
            _suppressFocusInterrupt = true;
            _screenSettleUntil = Time.unscaledTime + SettleSeconds;
        }

        // DE drives focus through NavigationManager, but at a freshly opened menu the uGUI EventSystem
        // records the selection a frame or more before NavigationManager does; fall back to it so the
        // initial focus is announced during that window (the dev FocusInspector/InputInjector do the same).
        private static Selectable CurrentSelectable()
        {
            var nav = NavigationManager.Singleton;
            Selectable sel = nav != null ? nav.GetCurrentSelectedSelectable() : null;
            if (sel != null)
                return sel;

            EventSystem es = EventSystem.current;
            GameObject go = es != null ? es.currentSelectedGameObject : null;
            return go != null ? go.GetComponent<Selectable>() : null;
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
