namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// The keys of the UI-category input actions the navigator consumes. The module registers
    /// <c>InputAction</c>s under these keys (in <see cref="InputCategory.UI"/>) and routes a fired one to
    /// <see cref="Navigator.Handle"/>, which switches on them. Shared here so registration and dispatch
    /// agree on one set of names.
    /// </summary>
    public static class UiActions
    {
        public const string Up = "ui.up";
        public const string Down = "ui.down";
        public const string Left = "ui.left";
        public const string Right = "ui.right";
        public const string Next = "ui.next";     // Tab
        public const string Prev = "ui.prev";     // Shift+Tab
        public const string Activate = "ui.activate";
        public const string Secondary = "ui.secondary"; // Backspace: a focused element's context action
        public const string Back = "ui.back";
        public const string Home = "ui.home";
        public const string End = "ui.end";
    }
}
