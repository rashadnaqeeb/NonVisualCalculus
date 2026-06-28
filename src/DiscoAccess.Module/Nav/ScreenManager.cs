using System.Collections.Generic;
using DiscoAccess.Core.Modularity;
using DiscoAccess.Core.UI.Nav;
using Sunshine.Views;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// Resolves the active screen from <c>ViewsPagesBridge.Current</c> each frame (while focus mode owns
    /// the keyboard) and, on a change, rebuilds that screen's tree and attaches the navigator to it,
    /// speaking the screen name then the landing control. Holds the one navigator and routes UI input
    /// into it via <see cref="Dispatch"/>. Only screens with a registered <see cref="Screen"/> are
    /// navigable; others detach (silent) for now.
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly IModHost _host;
        private readonly TraditionalNavigator _nav;
        private readonly Dictionary<ViewType, Screen> _screens = new Dictionary<ViewType, Screen>();

        private ViewType _attachedView;
        private bool _haveAttached;

        public ScreenManager(IModHost host)
        {
            _host = host;
            _nav = new TraditionalNavigator((text, interrupt) => _host.Speech.Speak(text, interrupt));
            Register(new MainMenuScreen());
        }

        private void Register(Screen screen) => _screens[screen.ViewType] = screen;

        /// <summary>Route a fired UI action into the navigator. Returns whether it was consumed.</summary>
        public bool Dispatch(string actionKey) => _nav.Handle(actionKey);

        /// <summary>Drop the attached screen so the next <see cref="Tick"/> re-attaches and re-announces
        /// (called when focus mode turns off, so re-entry reads the screen again).</summary>
        public void Reset()
        {
            _haveAttached = false;
            _nav.Attach(null);
        }

        public void Tick()
        {
            ViewType view = ViewsPagesBridge.Current;
            if (_haveAttached && view == _attachedView) return; // same screen; tree stands
            _attachedView = view;
            _haveAttached = true;

            if (_screens.TryGetValue(view, out Screen screen))
            {
                _nav.Attach(screen.BuildRoot(_host));
                _host.Speech.Speak(screen.ScreenName, interrupt: true); // supersedes; the landing queues behind
                _nav.AnnounceCurrent();
            }
            else
            {
                _nav.Attach(null); // unsupported under focus mode for now
            }
        }
    }
}
