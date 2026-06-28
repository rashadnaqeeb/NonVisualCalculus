namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>A focus-move direction (arrow keys).</summary>
    public enum NavDirection { Up, Down, Left, Right }

    /// <summary>
    /// Container shape - how a navigator traverses it.
    /// VerticalList/HorizontalList: arrows move among items; the whole container is one Tab-stop.
    /// Panel: Tab/Shift-Tab traverse its focusable descendants (WinForms-style); arrows do nothing.
    /// Tree and Grid exist in the reference design and will be added when a screen needs them.
    /// </summary>
    public enum ContainerShape { VerticalList, HorizontalList, Panel }
}
