using System.Collections.Generic;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// A 2D grid of homogeneous cells navigated with a cell cursor: Up/Down change row, Left/Right change
    /// column, and the whole grid is one Tab-stop. Unlike <see cref="Table"/> (records with fixed action
    /// columns, where only the changed axis is announced), every grid cell is self-describing and reads its
    /// full focus text on each move. Cells are added a row at a time and registered as ordinary focusable
    /// children (in row-major order, so the inherited first/last-focusable scans and the type-ahead search
    /// still work); the row/column geometry the grid navigator needs is held alongside. Movement and the
    /// announce live in <see cref="TraditionalNavigator"/>; this type is just the structure.
    /// </summary>
    public sealed class Grid : Container
    {
        private readonly List<List<UIElement>> _rows = new List<List<UIElement>>();
        private int _cols;

        public Grid(string? label = null, bool raggedColumns = false) : base(ContainerShape.Grid, label)
            => RaggedColumns = raggedColumns;

        public int RowCount => _rows.Count;
        public int ColCount => _cols;

        /// <summary>Whether each column is an independent list of its own length (a category grid), as
        /// opposed to a rectangular grid where every row spans every column. In a ragged grid a horizontal
        /// move switches column and lands on the focusable cell nearest the current row (the shorter column
        /// is clamped), rather than scanning sideways past a gap to the next focusable column. Short columns
        /// are padded with non-focusable cells so every category keeps its fixed column index.</summary>
        public bool RaggedColumns { get; }

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

        /// <summary>The (row, col) of a cell, or false if it is not in the grid.</summary>
        public bool TryCoords(UIElement cell, out int row, out int col)
        {
            for (int r = 0; r < _rows.Count; r++)
                for (int c = 0; c < _rows[r].Count; c++)
                    if (_rows[r][c] == cell) { row = r; col = c; return true; }
            row = 0; col = 0; return false;
        }
    }
}
