namespace DiscoAccess.Core.Modularity
{
    /// <summary>
    /// A dev-only seam the reloadable module exposes so the host's dev server can drive and inspect the
    /// mod's OWN UI navigation. The game-level input injector drives DE's NavigationManager, which our
    /// navigator bypasses (it owns the keyboard and reads its own input), so without this the dev server
    /// cannot exercise a migrated screen or the popup overlay. The host probes the loaded module for this
    /// by cast; a module that does not implement it leaves the dev server on its game-level fallback. Loaded
    /// in the default context (like <see cref="IModModule"/>) so its identity is stable across the boundary.
    /// Not used in normal play.
    /// </summary>
    public interface IDevDriver
    {
        /// <summary>Dispatch one semantic UI action (a <c>UiActions</c> key) into our navigator. Returns a
        /// status line when our navigator is driving (it owns the keyboard and no text field has it), or
        /// null when it is not, so the caller falls back to the game's own input injector.</summary>
        string DispatchUi(string action);

        /// <summary>Describe our navigator's live state: keyboard ownership, whether the popup overlay is
        /// up, the focus path with labels and roles, and the focused leaf. Independent of the game's own
        /// selection (which the /focus endpoint reports).</summary>
        string DescribeNav();

        /// <summary>Fire one world-layer or Global action (a <c>WorldActions</c> key like
        /// "world.interact", or a global toggle like "mod.bookmarks") through its registered handler -
        /// the same code path a real key press runs. A Global action fires from anywhere, like its live
        /// key; for the rest, returns null when the world reader does not own the keyboard (a menu or
        /// text edit has it), so the caller reports why nothing fired rather than firing into the wrong
        /// layer.</summary>
        string DriveWorld(string actionKey);

        /// <summary>Type text into a live mod-owned text edit (a bookmark name), the headless counterpart
        /// of the OS-typed characters the module reads for it. Returns a status line, or null when no mod
        /// edit is active, so the caller falls back to the game-field text injector.</summary>
        string TypeText(string text);
    }
}
