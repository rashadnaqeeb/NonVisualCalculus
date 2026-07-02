using System.Collections.Generic;
using System.Text;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// Drives our own keyboard UI navigation, the default way menus are read. Each frame it resolves the
    /// active screen from <c>ViewsPagesBridge.Current</c> and, for a screen with a registered
    /// <see cref="Screen"/>, takes the keyboard (mutes the game's menu input), builds that screen's tree
    /// once on entry, attaches the navigator, and speaks the screen name then the landing control. While
    /// the same screen stands it ticks the screen so a rich screen can refresh its dynamic content.
    ///
    /// We OWN the keyboard with one clean, reversible lever: disabling <c>InControl.InputManager</c>, the
    /// upstream input source the game reads ALL its menu input from. Confirmed live that disabling it
    /// kills every menu key at once - directions, submit, AND the Escape/back the per-view
    /// <c>CloseOnEscapeKey</c> path and a NavigationManager toggle both failed to mute. Our own input
    /// polls <c>UnityEngine.Input</c> directly, independent of InControl, so our keys keep working; and
    /// activation calls <c>NavigationManager.Select</c>/<c>Submit</c> directly (not through input), so it
    /// still runs. The lever is reasserted each frame in case the game re-enables InControl (e.g. on
    /// focus/device change).
    ///
    /// The lever is taken while we drive a registered screen OR the popup overlay. DE's shared
    /// confirmation/error/quit popup (<see cref="PopupOverlay"/>) floats over any view rather than matching a
    /// ViewType, so it is resolved by visibility ahead of the view's own screen and driven as a navigable
    /// overlay; the screen underneath is kept attached and resumed in place when the popup closes. A screen
    /// with no registered <see cref="Screen"/> (not yet migrated) and no popup hands the keyboard back to the
    /// game, so the legacy focus-follower fallback keeps working while screens migrate one at a time.
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly IModHost _host;
        private readonly TraditionalNavigator _nav;
        // Resolved in registration order: the first screen whose ViewType matches and whose AppliesNow is
        // true wins, so a more specific screen (the pause menu) is registered before its fallback (the
        // title menu), which share a ViewType.
        private readonly List<Screen> _screens = new List<Screen>();

        // The screen our navigator is currently attached to (null when detached). Tracked as the instance,
        // not just its ViewType, so switching between two screens that share a ViewType (the title menu and
        // the in-game pause menu, both MAINMENU) rebuilds and re-announces.
        private Screen _attachedScreen;
        // Whether we drove (owned the keyboard) last frame, so the lever is restored exactly once when we
        // stop driving rather than forced every frame (which would fight a lock the game itself set).
        private bool _wasOwning;
        // The attached screen's root, kept so the screen under a popup resumes in place (its tree and
        // remembered focus intact) when the popup closes, without a rebuild that would lose the player's
        // place. Null when no registered screen is attached.
        private Container _baseRoot;
        // Whether the popup overlay drove last frame, so the screen underneath re-announces its focus when
        // the popup closes (the popup left our navigator pointed at its own tree, the screen's untouched but
        // unspoken). Paired with the live message to re-announce when one prompt replaces another in place.
        private bool _popupActive;
        private string _lastPopupMessage;
        // Whether ViewsPagesBridge.Current has ever returned: before the view system finishes booting it
        // throws, which is expected; once it has worked, a later throw is a real failure.
        private bool _bridgeReady;
        // Whether the "not ready yet" throw has been logged once, so the boot transient surfaces but does
        // not spam every frame until the view system is up.
        private bool _warnedNotReady;
        // The screen whose build last threw, so the failure is logged once rather than every frame while
        // the broken screen stays up. Cleared on any successful build.
        private Screen _buildFailed;

        // The mod's own settings menu when open (F12), null when closed. A mod overlay floats above the
        // game and maps to no ViewType, so it is driven ahead of any view or popup; _modMenuAttached gates
        // its one-time build/announce, and _modMenuWasActive lets the frame after close resume the screen
        // underneath (re-attach our navigator and re-announce its focus, which the overlay left untouched).
        private ModMenuScreen _modMenu;
        private bool _modMenuAttached;
        private bool _modMenuWasActive;

        /// <summary>Whether our navigator is driving a registered screen this frame (lever taken). Set by
        /// <see cref="Tick"/> before input is polled, so the input layer can gate UI keys on it.</summary>
        public bool OwnsKeyboard { get; private set; }

        /// <summary>Whether the view system is up (ViewsPagesBridge.Current read without throwing) this
        /// frame. False during early boot; the focus-follower fallback must skip its own
        /// ViewsPagesBridge/NavigationManager reads until this is true or they throw too.</summary>
        public bool ViewReady { get; private set; }

        /// <summary>Whether type-ahead search is enabled on the attached screen (default true when none).</summary>
        public bool TypeAheadEnabled => _attachedScreen?.TypeAheadEnabled ?? true;

        /// <summary>Whether the attached screen wants the Status keys (status reads, quick-heals) live
        /// (default false when none).</summary>
        public bool StatusKeysActive => _attachedScreen?.WantsStatusKeys ?? false;

        public ScreenManager(IModHost host)
        {
            _host = host;
            _nav = new TraditionalNavigator((text, interrupt) => _host.Speech.Speak(text, interrupt));
            // PauseMenuScreen before MainMenuScreen: both are MAINMENU, and the pause menu is the more
            // specific match (the title menu is the fallback).
            Register(new PauseMenuScreen());
            Register(new MainMenuScreen());
            Register(new OptionsScreen());
            Register(new LoadGameScreen());
            Register(new SaveGameScreen());
            Register(new ArchetypeScreen());
            Register(new AdjustAbilitiesScreen());
            Register(new SignatureSkillScreen());
            // The begin prompt a new game (and the ledger dream) waits on. SPECIAL is otherwise a
            // transition state no screen matches; this one applies only while its button is live.
            Register(new BeginScreen());
            Register(new CharacterSheetScreen());
            Register(new ThoughtCabinetScreen());
            Register(new JournalScreen());
            Register(new InventoryScreen());
            Register(new DialogueScreen());
            Register(new HelpOverlayScreen());
            // The world's loot panel: CLEAR (free-roam) has no screen of its own, so this applies only
            // while the panel is up and the world reader keeps the keyboard the rest of the time.
            Register(new ContainerPanelScreen());
        }

        private void Register(Screen screen) => _screens.Add(screen);

        // The screen for this view: first registered whose ViewType matches and which applies now.
        private Screen Resolve(ViewType view)
        {
            foreach (Screen s in _screens)
                if (s.ViewType == view && s.AppliesNow())
                    return s;
            return null;
        }

        /// <summary>Route a fired UI action into the navigator. Returns whether it was consumed.</summary>
        public bool Dispatch(string actionKey) => _nav.Handle(actionKey);

        /// <summary>Dev introspection: our navigator's live state, independent of the game's selection -
        /// keyboard ownership, the popup overlay, the attached screen, and the focus path with the text each
        /// node would speak. Reads live UI; caches nothing.</summary>
        public string DescribeNav()
        {
            var sb = new StringBuilder();
            sb.Append("owns keyboard: ").Append(OwnsKeyboard).Append('\n');
            sb.Append("popup overlay: ").Append(_popupActive).Append('\n');
            sb.Append("screen: ").Append(_attachedScreen != null ? _attachedScreen.GetType().Name : "(none)").Append('\n');

            IReadOnlyList<UIElement> path = _nav.FocusPath;
            if (path.Count == 0)
            {
                sb.Append("focus: (none)\n");
                return sb.ToString();
            }

            var parts = new List<string>(path.Count);
            foreach (UIElement e in path)
            {
                string t = e.GetFocusText();
                parts.Add(string.IsNullOrEmpty(t) ? "(" + e.GetType().Name + ")" : t);
            }
            sb.Append("path: ").Append(string.Join(" > ", parts)).Append('\n');
            UIElement cur = _nav.Current;
            sb.Append("current: ").Append(cur != null ? cur.GetFocusText() : "(none)").Append('\n');
            return sb.ToString();
        }

        /// <summary>Whether the navigator's type-ahead buffer holds a character (the raw reader gates a
        /// typed Space on this so a lone Space is not swallowed into an empty search).</summary>
        public bool SearchHasBuffer => _nav.SearchHasBuffer;

        /// <summary>Feed one typed character into the navigator's type-ahead search.</summary>
        public void TypeSearch(char c) => _nav.TypeSearchChar(c);

        /// <summary>Delete the last character from the navigator's type-ahead search.</summary>
        public void BackspaceSearch() => _nav.BackspaceSearch();

        /// <summary>Silently drop any live type-ahead search (the keyboard left our navigator).</summary>
        public void ClearSearch() => _nav.ClearSearch(announce: false);

        /// <summary>Re-enable the game's input for now, so an Escape our navigator did not consume (a screen
        /// with no Back of its own, like the title menu) reaches the game's own Escape handling - nothing at
        /// the title, resume in the pause menu. We never swallow Escape into a silent no-op the game would
        /// have acted on. Reasserted (re-disabled) next Tick while we still own the screen, so this is a
        /// one-frame hand-back, not a release.</summary>
        public void DeferEscapeToGame() => InControl.InputManager.Enabled = true;

        /// <summary>Open the mod menu if closed, close it if open. Driven by the F12 global hotkey. The
        /// open/close takes effect on the next <see cref="Tick"/>.</summary>
        public void ToggleModMenu()
        {
            if (_modMenu != null) _modMenu = null;
            else _modMenu = new ModMenuScreen();
        }

        // The overlay's Back action (Escape) closes the menu; the next Tick resumes the screen underneath.
        private void CloseModMenu() => _modMenu = null;

        /// <summary>Resolve the active screen and set keyboard ownership for this frame. Call before
        /// polling input. <paramref name="editEnded"/> means a text edit just committed, so the standing
        /// screen re-reads the focused control once.</summary>
        public void Tick(bool editEnded)
        {
            // The mod menu floats above everything when open: it owns the keyboard, drives its own navigable
            // tree, and closes on Escape (or F12 again). It maps to no game view, so it is resolved before
            // the view system and the popup overlay. The screen underneath stays attached (untouched) and
            // resumes when the menu closes.
            if (_modMenu != null)
            {
                InControl.InputManager.Enabled = false; // reasserted each frame, like the screen path
                _wasOwning = true;
                OwnsKeyboard = true;
                if (!_modMenuAttached)
                {
                    _nav.Attach(_modMenu.BuildRoot(_host, CloseModMenu));
                    _host.Speech.Speak(_modMenu.Title, interrupt: true); // supersedes; the landing queues behind
                    _nav.AnnounceCurrent();
                    _modMenuAttached = true;
                }
                _modMenuWasActive = true;
                return;
            }
            // The menu just closed: the screen under it (if registered) resumes below, re-attaching our
            // navigator to its root and re-announcing the focus the overlay left untouched but unspoken.
            bool modMenuJustClosed = _modMenuWasActive;
            _modMenuWasActive = false;
            _modMenuAttached = false;

            // The popup overlay floats over any view; while up, our navigator drives it ahead of the view's
            // own screen. It needs no view system, so it is resolved before the view-ready gate below. The
            // screen underneath stays attached (_attachedScreen/_baseRoot untouched) so it resumes on close.
            if (PopupOverlay.IsShowing())
            {
                InControl.InputManager.Enabled = false; // reasserted each frame, like the screen path
                _wasOwning = true;
                OwnsKeyboard = true;
                string msg = PopupOverlay.Message();
                // Build and announce on open, and again when one prompt replaces another in place (the
                // controller is reused, so its visibility never drops between two sequential prompts).
                if (!_popupActive || msg != _lastPopupMessage)
                {
                    _nav.Attach(PopupOverlay.BuildRoot());
                    _host.Speech.Speak(msg, interrupt: true); // supersedes; the landing button queues behind
                    _nav.AnnounceCurrent();
                    _popupActive = true;
                    _lastPopupMessage = msg;
                }
                return;
            }

            // The popup just closed: the screen under it (if registered) resumes below. Flag it so the
            // standing-screen branch re-attaches our navigator to that screen's root and re-announces the
            // focus the popup left untouched but unspoken.
            bool popupJustClosed = _popupActive;
            _popupActive = false;
            _lastPopupMessage = null;

            if (!TryGetView(out ViewType view))
            {
                // The view system is not ready yet (early boot): leave the game its input and detach.
                ViewReady = false;
                OwnsKeyboard = false;
                if (_attachedScreen != null) { _nav.Attach(null); _attachedScreen = null; }
                return;
            }
            ViewReady = true;

            Screen screen = Resolve(view);
            bool registered = screen != null;
            bool own = registered;

            // Take the lever only while we actively drive, reasserted each frame (the game re-enables
            // InControl on focus/device changes), and restore it exactly once when we stop. On frames we
            // never owned, leave the game's own input state alone so we don't fight a lock it set (a
            // cutscene, loading, or unrecognized modal).
            if (own) InControl.InputManager.Enabled = false;
            else if (_wasOwning) InControl.InputManager.Enabled = true;
            _wasOwning = own;
            OwnsKeyboard = own;

            if (!own)
            {
                // Detach only when leaving a registered screen entirely, not for a transient popup over
                // one: keeping the tree and remembered focus lets the screen resume when the popup closes.
                if (!registered && _attachedScreen != null)
                {
                    _nav.Attach(null);
                    _attachedScreen = null;
                    _baseRoot = null;
                }
                else if (modMenuJustClosed)
                    _nav.Attach(null); // menu closed over an unowned view; drop its now-stale tree
                return;
            }

            if (screen == _attachedScreen)
            {
                // A popup or the mod menu just closed over us: it left our navigator pointed at its own tree,
                // so restore it to this screen's root (kept intact underneath) before refreshing/announcing.
                if (popupJustClosed || modMenuJustClosed)
                    _nav.Attach(_baseRoot);

                // Refresh the screen's dynamic content FIRST (a rich screen may rebuild its tree and re-home
                // focus), THEN announce once. Announcing after the update means we read the live focus, not a
                // cell the rebuild just destroyed, and the single announce avoids double-speaking. Speak when:
                // a popup or the mod menu just closed over us (focus untouched but unheard), a text edit just
                // committed (hear the result and landing), or the update re-homed focus this frame.
                bool refocused = screen.OnUpdate(_host, _nav);
                if (popupJustClosed || modMenuJustClosed || editEnded || refocused)
                    _nav.AnnounceCurrent();
                return;
            }

            // Build, announce, then record the attach. A BuildRoot throw is the dangerous case: we have
            // already taken the lever (InControl is off), so an uncaught throw would leave a keyboard-only
            // player with the game muted and our navigator never built - a dead keyboard. Catch it, hand the
            // keyboard back, and detach, so the broken screen falls back to the game's own input. (A screen
            // with no Back is fine to own - the title menu has none by design; an unconsumed Escape there is
            // handed back to the game by the pump, see DeferEscapeToGame.)
            try
            {
                Container root = screen.BuildRoot(_host);
                _nav.Attach(root);
                _host.Speech.Speak(screen.ScreenName, interrupt: true); // supersedes; the landing queues behind
                _nav.AnnounceCurrent();
                _attachedScreen = screen;
                _baseRoot = root; // kept so the screen resumes in place under a popup
                _buildFailed = null;
            }
            catch (System.Exception e)
            {
                if (_buildFailed != screen)
                {
                    _host.LogError($"ScreenManager: building {screen.GetType().Name} failed; handing the keyboard back to the game. {e}");
                    _buildFailed = screen;
                }
                InControl.InputManager.Enabled = true;
                _wasOwning = false;
                OwnsKeyboard = false;
                _nav.Attach(null);
                _attachedScreen = null;
                _baseRoot = null;
            }
        }

        // Read the current view, treating the early-boot "not ready yet" throw as a transient (silent, the
        // game keeps its input) and a post-ready throw as a real failure worth surfacing.
        private bool TryGetView(out ViewType view)
        {
            try
            {
                view = ViewsPagesBridge.Current;
                _bridgeReady = true;
                return true;
            }
            catch (System.Exception e)
            {
                view = default;
                // Expected only during early boot. Surface the first occurrence (so a view system that
                // never comes up is visible) and any failure once it has worked, but stay silent for the
                // rest of the boot transient so it does not spam every frame.
                if (_bridgeReady || !_warnedNotReady)
                {
                    _warnedNotReady = true;
                    _host.LogWarning("ScreenManager: ViewsPagesBridge.Current not ready: " + e.Message);
                }
                return false;
            }
        }

        /// <summary>Hand the keyboard back to the game and detach, for module teardown so a reload never
        /// leaves InControl disabled.</summary>
        public void HandBack()
        {
            InControl.InputManager.Enabled = true;
            _nav.Attach(null);
            _attachedScreen = null;
            _baseRoot = null;
            _popupActive = false;
            _lastPopupMessage = null;
            _modMenu = null;
            _modMenuAttached = false;
            _modMenuWasActive = false;
            _wasOwning = false;
            OwnsKeyboard = false;
        }
    }
}
