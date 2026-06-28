using DiscoAccess.Core.Modularity;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The master switch for our keyboard UI navigation. While active we OWN the keyboard with one clean,
    /// reversible lever: disabling <c>InControl.InputManager</c>, the upstream input source the game reads
    /// ALL its menu input from. Confirmed live that disabling it kills every menu key at once -
    /// directions, submit, AND the Escape/back that the per-view <c>CloseOnEscapeKey</c> path and a
    /// NavigationManager toggle both failed to mute. Our own input polls <c>UnityEngine.Input</c>
    /// directly, independent of InControl, so our keys keep working; and activation calls
    /// <c>NavigationManager.Select</c>/<c>Submit</c> directly (not through input), so it still runs. The
    /// lever is reasserted each frame in case the game re-enables InControl (e.g. on focus/device change).
    /// </summary>
    public static class FocusMode
    {
        private static IModHost _host;

        public static bool Active { get; private set; }

        public static void Init(IModHost host) => _host = host;

        public static void Toggle() => Set(!Active);

        public static void Set(bool on)
        {
            if (on == Active) return;
            Active = on;
            ApplyLever();
        }

        /// <summary>While active, reassert the lever (the game may re-enable InControl on focus changes).</summary>
        public static void Tick()
        {
            if (Active) ApplyLever();
        }

        private static void ApplyLever() => InControl.InputManager.Enabled = !Active;
    }
}
