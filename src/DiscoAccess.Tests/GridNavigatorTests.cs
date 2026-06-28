using System.Collections.Generic;
using DiscoAccess.Core.UI.Nav;
using Xunit;

namespace DiscoAccess.Tests
{
    public class GridNavigatorTests
    {
        // A grid cell carrying a fixed column header and row text, recording activations and focus lands.
        private sealed class Cell : GridCell
        {
            private readonly string _col;
            private readonly string _row;
            private readonly bool _focusable;
            public int Activations;
            public int Focuses;

            public Cell(string col, string row, bool focusable = true)
            {
                _col = col;
                _row = row;
                _focusable = focusable;
            }

            public override bool CanFocus => _focusable;
            public override string ColumnHeader => _col;
            public override string RowText => _row;
            public override void OnFocused() => Focuses++;

            public override IEnumerable<ElementAction> GetActions()
            {
                yield return new ElementAction(ActionIds.Activate, () => Activations++);
            }
        }

        private readonly List<string> _spoken = new List<string>();
        private TraditionalNavigator NewNav() => new TraditionalNavigator((t, i) => _spoken.Add(t));

        // Panel root > a save grid of [Load, Delete] columns, one row per "Save n". A deletable flag per
        // row drives whether that row's Delete cell is focusable (non-deletable saves: auto/quick/readonly).
        private static (Container root, Grid grid, Cell[][] cells) SaveGrid(params bool[] deletable)
        {
            var root = new Container(ContainerShape.Panel);
            var grid = new Grid();
            var cells = new Cell[deletable.Length][];
            for (int r = 0; r < deletable.Length; r++)
            {
                var load = new Cell("Load", $"Save {r + 1}");
                var del = new Cell("Delete", $"Save {r + 1}", deletable[r]);
                cells[r] = new[] { load, del };
                grid.AddRow(load, del);
            }
            root.Add(grid);
            return (root, grid, cells);
        }

        [Fact]
        public void Attach_LandsOnFirstRowLoad_AnnouncesColumnThenRow()
        {
            var (root, _, cells) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);

            Assert.Same(cells[0][0], nav.Current);
            nav.AnnounceCurrent();
            Assert.Equal(new[] { "Load, Save 1" }, _spoken);
            Assert.Equal(1, cells[0][0].Focuses); // the landing synced focus
        }

        [Fact]
        public void Down_KeepsColumn_AnnouncesOnlyRow()
        {
            var (root, _, cells) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Down));
            Assert.Same(cells[1][0], nav.Current);
            Assert.Equal(new[] { "Save 2" }, _spoken);
        }

        [Fact]
        public void Right_KeepsRow_AnnouncesOnlyColumn()
        {
            var (root, _, cells) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Right));
            Assert.Same(cells[0][1], nav.Current);
            Assert.Equal(new[] { "Delete" }, _spoken);

            Assert.True(nav.Handle(UiActions.Left));
            Assert.Same(cells[0][0], nav.Current);
            Assert.Equal("Load", _spoken[^1]);
        }

        [Fact]
        public void DownInDeleteColumn_AnnouncesOnlyRow()
        {
            var (root, _, cells) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Right); // into the Delete column
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Down));
            Assert.Same(cells[1][1], nav.Current);
            Assert.Equal(new[] { "Save 2" }, _spoken);
        }

        [Fact]
        public void RightAtLastColumn_Consumes_DoesNotMove()
        {
            var (root, _, cells) = SaveGrid(true);
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Right); // on Delete now (last column)
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Right)); // consumed, but nowhere to go
            Assert.Same(cells[0][1], nav.Current);
            Assert.Empty(_spoken);
        }

        [Fact]
        public void UpAtTop_Consumes_DoesNotMove()
        {
            var (root, _, cells) = SaveGrid(true, true);
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Up)); // grid consumes even at the edge (no wrap)
            Assert.Same(cells[0][0], nav.Current);
            Assert.Empty(_spoken);
        }

        [Fact]
        public void Right_OnNonDeletableSave_StaysOnLoad()
        {
            var (root, _, cells) = SaveGrid(false); // a single non-deletable save
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Right)); // its Delete cell is not focusable
            Assert.Same(cells[0][0], nav.Current);
            Assert.Empty(_spoken);
        }

        [Fact]
        public void DownInDeleteColumn_SkipsNonDeletableSave()
        {
            var (root, _, cells) = SaveGrid(true, false, true); // middle save not deletable
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Right); // Delete column, row 0
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Down)); // row 1's Delete is unfocusable -> skip to row 2
            Assert.Same(cells[2][1], nav.Current);
            Assert.Equal(new[] { "Save 3" }, _spoken);
        }

        [Fact]
        public void HomeAndEnd_JumpWithinColumn_AnnounceRow()
        {
            var (root, _, cells) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.End));
            Assert.Same(cells[2][0], nav.Current);
            Assert.Equal("Save 3", _spoken[^1]);

            Assert.True(nav.Handle(UiActions.Home));
            Assert.Same(cells[0][0], nav.Current);
            Assert.Equal("Save 1", _spoken[^1]);
        }

        [Fact]
        public void Right_SkipsNonFocusableMiddleColumn_ToReachThird()
        {
            // The save menu's create-new row: [Save] [Delete (not deletable)] [Rename]. Right from Save
            // should skip the non-focusable Delete and land on Rename.
            var root = new Container(ContainerShape.Panel);
            var grid = new Grid();
            var save = new Cell("Save", "Row 1");
            var delete = new Cell("Delete", "Row 1", focusable: false);
            var rename = new Cell("Rename", "Row 1");
            grid.AddRow(save, delete, rename);
            root.Add(grid);
            var nav = NewNav();
            nav.Attach(root);
            _spoken.Clear();

            Assert.True(nav.Handle(UiActions.Right));
            Assert.Same(rename, nav.Current);
            Assert.Equal(new[] { "Rename" }, _spoken);
        }

        [Fact]
        public void Activate_FiresFocusedCellAction()
        {
            var (root, _, cells) = SaveGrid(true, true);
            var nav = NewNav();
            nav.Attach(root);

            Assert.True(nav.Handle(UiActions.Activate));
            Assert.Equal(1, cells[0][0].Activations);
            Assert.Equal(0, cells[0][1].Activations);
        }

        [Fact]
        public void Clear_ResetsRowsSoFocusReHomes()
        {
            var (root, grid, _) = SaveGrid(true, true, true);
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Down); // focus row 1

            // A delete refreshes the list: the grid is cleared and refilled with fewer rows.
            grid.Clear();
            Assert.Equal(0, grid.RowCount);
            var newFirst = new Cell("Load", "Save A");
            grid.AddRow(newFirst, new Cell("Delete", "Save A"));

            Assert.True(nav.EnsureFocusValid()); // old focus orphaned -> re-homed
            Assert.Same(newFirst, nav.Current);
        }

        // Panel root > a [Load, Delete] grid with one named row each; a deletable flag per row drives
        // whether that row's Delete cell is focusable. Distinct names so type-ahead can pick a row.
        private static (Container root, Grid grid, Cell[][] cells) NamedGrid(params (string name, bool deletable)[] rows)
        {
            var root = new Container(ContainerShape.Panel);
            var grid = new Grid();
            var cells = new Cell[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                var load = new Cell("Load", rows[r].name);
                var del = new Cell("Delete", rows[r].name, rows[r].deletable);
                cells[r] = new[] { load, del };
                grid.AddRow(load, del);
            }
            root.Add(grid);
            return (root, grid, cells);
        }

        [Fact]
        public void TypeSearch_InGrid_MatchesWholeRow_NotEachCell()
        {
            var (root, _, cells) = NamedGrid(("Alpha", true), ("Bravo", true), ("Charlie", true));
            var nav = NewNav();
            nav.Attach(root); // lands on Alpha's Load (row 0, col 0)

            nav.TypeSearchChar('a'); // Alpha (starts), then Bravo, Charlie (contain 'a') - one result per row
            Assert.Same(cells[0][0], nav.Current);

            // Stepping the results moves to the NEXT ROW's Load, not Alpha's own Delete cell (which would be
            // a separate result if the grid searched per cell instead of per row).
            Assert.True(nav.Handle(UiActions.Down));
            Assert.Same(cells[1][0], nav.Current); // Bravo's Load, column kept
        }

        [Fact]
        public void TypeSearch_InGrid_KeepsTheFocusedColumn()
        {
            var (root, _, cells) = NamedGrid(("Alpha", true), ("Bravo", true));
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Right); // move to Alpha's Delete column

            nav.TypeSearchChar('b'); // jump to Bravo, keeping the Delete column
            Assert.Same(cells[1][1], nav.Current);
        }

        [Fact]
        public void TypeSearch_InGrid_FallsBackWhenColumnAbsentInRow()
        {
            var (root, _, cells) = NamedGrid(("Alpha", true), ("Bravo", false)); // Bravo not deletable
            var nav = NewNav();
            nav.Attach(root);
            nav.Handle(UiActions.Right); // Alpha's Delete column

            nav.TypeSearchChar('b'); // Bravo has no focusable Delete -> land on its Load instead
            Assert.Same(cells[1][0], nav.Current);
        }

        [Fact]
        public void AfterGridSearch_LeftRightMoveBetweenRowButtons()
        {
            var (root, _, cells) = NamedGrid(("Alpha", true), ("Bravo", true));
            var nav = NewNav();
            nav.Attach(root);
            nav.TypeSearchChar('b'); // lands on Bravo's Load, search live
            Assert.True(nav.SearchActive);

            // Right is not a search key: it ends the search and does a normal grid move to the next column.
            Assert.True(nav.Handle(UiActions.Right));
            Assert.Same(cells[1][1], nav.Current); // Bravo's Delete
            Assert.False(nav.SearchActive);
        }
    }
}
