using System.Collections.Generic;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// A table of cells navigated with a cell cursor: rows are records, columns are fixed fields or
    /// actions. Up/Down change row, Left/Right change column, and the whole table is one Tab-stop. Cells
    /// are added a row at a time and registered as ordinary focusable children (in row-major order, so the
    /// inherited first/last-focusable scans still work); the row/column geometry the table navigator needs
    /// is held alongside. Movement and the axis-aware announce live in <see cref="TraditionalNavigator"/>;
    /// this type is just the structure.
    /// </summary>
    public sealed class Table : Container
    {
        private readonly List<List<UIElement>> _rows = new List<List<UIElement>>();
        private int _cols;

        public Table(string? label = null) : base(ContainerShape.Table, label) { }

        public int RowCount => _rows.Count;
        public int ColCount => _cols;

        /// <summary>Add a row of cells, left to right. Each cell is registered as a focusable child.</summary>
        public void AddRow(params UIElement[] cells)
        {
            var row = new List<UIElement>(cells.Length);
            foreach (var cell in cells)
            {
                Add(cell);
                row.Add(cell);
            }
            _rows.Add(row);
            if (row.Count > _cols) _cols = row.Count;
        }

        public override void Clear()
        {
            base.Clear();
            _rows.Clear();
            _cols = 0;
        }

        /// <summary>The cell at (row, col), or null when out of range.</summary>
        public UIElement? CellAt(int row, int col)
        {
            if (row < 0 || row >= _rows.Count || col < 0 || col >= _rows[row].Count)
                return null;
            return _rows[row][col];
        }

        /// <summary>The (row, col) of a cell, or false if it is not in the table.</summary>
        public bool TryCoords(UIElement cell, out int row, out int col)
        {
            for (int r = 0; r < _rows.Count; r++)
                for (int c = 0; c < _rows[r].Count; c++)
                    if (_rows[r][c] == cell) { row = r; col = c; return true; }
            row = 0; col = 0; return false;
        }
    }

    /// <summary>
    /// A cell in a <see cref="Table"/>. It exposes the two axis labels the table navigator speaks as the
    /// cursor moves: the column header (spoken when the column changes) and the row text (spoken when the
    /// row changes). The full focus message (screen entry, Tab landing) joins both, so a cell that lands
    /// focus from nowhere still reads its column and row together.
    /// </summary>
    public abstract class TableCell : UIElement
    {
        /// <summary>The column's heading word (e.g. an action), read live; spoken on a column move.</summary>
        public abstract string? ColumnHeader { get; }

        /// <summary>The row's text (e.g. the save line), read live; spoken on a row move.</summary>
        public abstract string? RowText { get; }

        public override string GetFocusText()
            => Text.SpokenLine.Join(ColumnHeader, RowText, Value);
    }
}
