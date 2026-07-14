namespace NonVisualCalculus.Core.UI.Nav
{
    /// <summary>A focus-move direction (arrow keys).</summary>
    public enum NavDirection { Up, Down, Left, Right }

    /// <summary>
    /// Container shape - how a navigator traverses it.
    /// VerticalList/HorizontalList: arrows move among items; the whole container is one Tab-stop.
    /// Table: a cell cursor over rows of records and fixed field/action columns (Up/Down change row,
    /// Left/Right change column); the whole table is one Tab-stop, and the navigator announces only the
    /// axis that changed (the column header on a column move, the row text on a row move).
    /// Grid: a cell cursor over a 2D arrangement of homogeneous cells (Up/Down change row, Left/Right
    /// change column); the whole grid is one Tab-stop, and unlike a Table every cell is self-describing and
    /// reads its full focus text on each move.
    /// Panel: Tab/Shift-Tab traverse its focusable descendants (WinForms-style); arrows do nothing.
    /// Tree exists in the reference design and will be added when a screen needs it.
    /// </summary>
    public enum ContainerShape { VerticalList, HorizontalList, Table, Grid, Panel }
}
