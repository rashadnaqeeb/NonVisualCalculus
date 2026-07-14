using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine.Views;
using UnityEngine;
using UnityEngine.UI;
using Table = NonVisualCalculus.Core.UI.Nav.Table;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// Shared base for the save and load screens, which are the same table over the game's one save file
    /// list: one row per save, two columns, the primary action (Load or Save) then Delete. Focus lands on
    /// the first save's primary cell; Up/Down move between saves keeping the column, Left/Right switch
    /// between the action and Delete on the focused save. The game has a single shared primary button and a
    /// single shared Delete button that act on the selected save, so each cell selects its save on focus
    /// and runs the matching button on activate. Saving or deleting refreshes the list, so this is a rich
    /// screen: <see cref="OnUpdate"/> rebuilds the table in place when the save count changes and re-homes
    /// focus. Subclasses supply the view, the spoken name, and which shared button is the primary action.
    /// </summary>
    public abstract class SaveLoadTableScreen : Screen
    {
        // The shared button for the primary column (Load in the load menu, Save in the save menu).
        protected abstract Button PrimaryButton(SaveLoadController ctrl);

        // The live table we built, and the save count it reflects, for detecting a save/delete in OnUpdate.
        // Held as a reference into our own tree, not cached game state.
        private Table _table;
        // A signature of the entry set the table was built from (each active entry's instance id, in order).
        // Detects not just count changes (save/delete) but in-place recreations: committing a rename
        // refreshes the list and rebuilds the entry GameObjects with the same count, which a count check
        // would miss, leaving the table holding destroyed Selectables that throw when acted on.
        private int _builtSignature;

        public override Container BuildRoot(IModHost host)
        {
            var root = new ScreenRoot();
            var ctrl = Object.FindObjectOfType<SaveLoadController>();
            if (ctrl == null)
            {
                host.LogWarning(GetType().Name + ": SaveLoadController not found; empty screen.");
                return root;
            }
            _table = new Table();
            Populate(host, ctrl);
            root.Add(_table);
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            var ctrl = Object.FindObjectOfType<SaveLoadController>();
            if (ctrl == null || _table == null)
                return false;
            // The save list changed (created, deleted, or rebuilt by a rename): rebuild and re-home focus,
            // keeping the player's place. Capture the focused cell's coordinates BEFORE the rebuild clears
            // the table, then steer the re-home to the same column and nearest surviving row so a rename or
            // delete does not dump the player back at the top of a long list.
            if (EntrySignature(ctrl) == _builtSignature)
                return false;

            int row = 0, col = 0;
            bool hadCoords = nav.Current != null && _table.TryCoords(nav.Current, out row, out col);
            Populate(host, ctrl);
            if (hadCoords)
            {
                UIElement target = NearestFocusable(row, col);
                if (target != null)
                    _table.SetFocusedChild(target); // EnsureFocusValid re-homes onto the table's remembered child
            }
            // Re-home onto that child (the rebuild orphaned the old one); the result reports whether focus
            // moved, so the ScreenManager announces the landing once.
            return nav.EnsureFocusValid();
        }

        // The focusable cell to land on after a rebuild, nearest the pre-rebuild position: the same column at
        // the same row if possible, else the closest focusable row in that column, else the table's first
        // focusable. Keeps the player on the action they were using (Save/Delete/Rename) and near their save.
        private UIElement NearestFocusable(int row, int col)
        {
            int last = _table.RowCount - 1;
            if (last < 0) return null;
            int start = row < 0 ? 0 : row > last ? last : row;
            for (int dist = 0; dist <= _table.RowCount; dist++)
            {
                UIElement down = _table.CellAt(start + dist, col);
                if (down != null && down.CanFocus) return down;
                UIElement up = _table.CellAt(start - dist, col);
                if (up != null && up.CanFocus) return up;
            }
            return null; // nothing focusable in that column; EnsureFocusValid falls back to first focusable
        }

        // Fill the table with one row per active save entry, in visual order. The shared primary and Delete
        // buttons are passed to every cell; each cell selects its own save before running them.
        private void Populate(IModHost host, SaveLoadController ctrl)
        {
            _table.Clear();
            Button primary = PrimaryButton(ctrl);
            Button delete = ctrl.DeleteButton;
            Transform content = ctrl.saveFileListContentPanel;
            if (content == null)
            {
                host.LogWarning(GetType().Name + ": save file list content panel is null; no saves.");
                _builtSignature = 0;
                return;
            }
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var entry = child.GetComponent<SaveGameListEntry>();
                if (entry == null) continue;
                var row = child.GetComponent<Selectable>();
                if (row == null) continue;
                BuildRow(host, _table, entry, row, primary, delete);
            }
            _builtSignature = EntrySignature(ctrl);
            if (_table.RowCount == 0)
                host.LogWarning(GetType().Name + ": built no save rows.");
        }

        /// <summary>Add one save's row of cells. The base is the primary action then Delete; the save menu
        /// overrides this to append a Rename cell (which needs <paramref name="host"/> to report a missing
        /// name field).</summary>
        protected virtual void BuildRow(IModHost host, Table table, SaveGameListEntry entry, Selectable row, Button primary, Button delete)
        {
            table.AddRow(new SaveActionCell(entry, row, primary), new DeleteCell(entry, row, delete));
        }

        // An order-sensitive hash of the active save entries' instance ids: changes when an entry is added,
        // removed, reordered, or recreated (a rename rebuilds the GameObjects), so the table rebuilds and
        // never keeps a destroyed Selectable.
        private static int EntrySignature(SaveLoadController ctrl)
        {
            Transform content = ctrl.saveFileListContentPanel;
            if (content == null) return 0;
            int hash = 17;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var entry = child.GetComponent<SaveGameListEntry>();
                if (entry == null) continue;
                hash = hash * 31 + entry.GetInstanceID();
            }
            return hash;
        }
    }
}
