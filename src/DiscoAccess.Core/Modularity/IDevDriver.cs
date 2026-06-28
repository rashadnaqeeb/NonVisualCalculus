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
    }
}
