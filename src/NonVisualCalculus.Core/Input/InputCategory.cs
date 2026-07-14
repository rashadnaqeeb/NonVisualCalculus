namespace NonVisualCalculus.Core.Input
{
    /// <summary>
    /// The input layer an action belongs to. Each frame the manager builds the set of LIVE categories
    /// from the active screens (via <see cref="InputManager.ActiveCategoriesProvider"/>, in priority
    /// order) plus <see cref="Global"/>, which is always on. Within the live set, an identical chord
    /// bound in two categories resolves to the higher-priority (earlier) one: that shadowing is how the
    /// same physical keys can mean menu navigation when a screen is focused and world-cursor movement
    /// when it is not. Conflict prevention therefore only matters WITHIN a category.
    /// </summary>
    public enum InputCategory
    {
        /// <summary>Always live, even with no screen focused (the focus-mode toggle, mod menu, global hotkeys).</summary>
        Global,

        /// <summary>Screen/menu navigation. Live when the focused screen declares it; routed into the
        /// active navigator rather than firing a handler directly.</summary>
        UI,

        /// <summary>The isometric world's own keys (cursor glide, the Enter interact verb, the world
        /// hotkeys). Live only while the world reader owns the keyboard (the player is in free-roam and
        /// no menu took it). Its actions fire their own handlers, the world reader's, rather
        /// than routing into the UI navigator.</summary>
        World,

        /// <summary>The world status readouts (time, money, health) and the two quick-heal keys
        /// (Left/Right) that stay useful outside free-roam. Live in free-roam (alongside <see cref="World"/>)
        /// and in any screen that declares it wants them (the conversation view), but not in menus where
        /// bare letters drive type-ahead. Like <see cref="World"/>, its actions fire their own handlers
        /// rather than routing through the navigator.</summary>
        Status,
    }
}
