using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using Sunshine;
using Sunshine.Metric;
using Sunshine.Views;
using UnityEngine;
using Container = NonVisualCalculus.Core.UI.Nav.Container;
using Grid = NonVisualCalculus.Core.UI.Nav.Grid;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The thought cabinet. Its root is a Panel with two Tab-stops: the slot grid (the twelve thought slots
    /// laid out three across, four down, in the game's own slot-index order, see <see cref="ThoughtSlotCell"/>)
    /// and the master list of every thought as a four-column category grid (see <see cref="ThoughtListCell"/>) -
    /// available, researched, forgotten and undiscovered, left to right, each column a list of its own length.
    /// Entry lands on the slot grid; Tab moves to the thoughts grid. Arrows move within each - Left/Right
    /// switch category (clamping onto a shorter column), Up/Down move through a category. Enter commits the
    /// entry's contextual action: unlock a buyable slot, forget a filled one, or internalize a thought from
    /// the list into a free slot (each the game's own way, with its confirmation where the game shows one).
    /// Escape closes the cabinet (<see cref="ScreenRoot"/>).
    ///
    /// "Researched" is the placed column: it holds every thought sitting in a slot, whether still cooking
    /// (it reads "researching, N percent") or finished ("researched"), since those are the thoughts being
    /// worked on. The other three columns are the gathered-but-unplaced, the forgotten, and the undiscovered.
    ///
    /// The slot cells wrap the twelve persistent <see cref="ThoughtSlot"/> objects and the list cells wrap
    /// the live entries, all read live, so a slot filling or emptying and a thought changing stage need no
    /// rebuild - the focused cell just reads its new state. The tree is rebuilt only when the slot or list
    /// set itself changes size (the panels appear a frame or two after the view transition, or a thought is
    /// gained while the cabinet is open), keyed on a signature that ignores per-action state churn so a
    /// commit never re-homes focus.
    /// </summary>
    public sealed class ThoughtCabinetScreen : Screen
    {
        public override ViewType ViewType => Sunshine.Views.ViewType.THOUGHTCABINET;
        public override string ScreenName => Strings.ScreenThoughtCabinet;

        private ScreenRoot _root;
        private IModHost _host;
        // Signature of the structural set the tree was built from (slot count and list-entry count): both
        // appear after the view transition, so this drives the in-place rebuild until they settle, and again
        // if a thought is gained or lost while the cabinet is open. Per-slot occupancy and per-thought stage
        // are deliberately excluded so a forget/unlock/internalize does not rebuild and lose the player's place.
        private int _builtSig;

        public override Container BuildRoot(IModHost host)
        {
            _host = host;
            _root = new ScreenRoot();
            _builtSig = -1;
            Populate();
            return _root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (Signature() == _builtSig)
                return false;
            Populate();
            return nav.EnsureFocusValid();
        }

        // Rebuild both tab-stops from live state: the slot grid first (so entry lands on it), then the master
        // list. Until the slots appear the grid is empty and contributes nothing focusable, so focus lands on
        // the grid the moment it fills.
        private void Populate()
        {
            _root.Clear();
            _builtSig = Signature();
            _root.Add(BuildSlotGrid());
            _root.Add(BuildThoughtGrid());
        }

        // The twelve slots as a 3-wide grid in slot-index order (the game's own row-major layout). An empty
        // grid (slots not yet present) is skipped by the navigator until OnUpdate fills it.
        private Grid BuildSlotGrid()
        {
            const int cols = 3;
            var grid = new Grid(Strings.ThoughtSlotGridLabel);
            ThoughtSlot[] slots = Slots();
            for (int r = 0; r * cols < slots.Length; r++)
            {
                var row = new List<UIElement>(cols);
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;
                    if (i < slots.Length && slots[i] != null)
                        row.Add(new ThoughtSlotCell(slots[i], _host));
                }
                if (row.Count > 0)
                    grid.AddRow(row.ToArray());
            }
            return grid;
        }

        // The master list as a four-column category grid: available, researched (placed), forgotten,
        // undiscovered, left to right. Each column is an independent list of its own length, in the game's
        // on-screen order within the category; shorter columns are padded with non-focusable gaps so each
        // category keeps its fixed column. Built ragged so Left/Right switch category and clamp onto a
        // shorter column rather than scanning across gaps.
        private Grid BuildThoughtGrid()
        {
            var columns = new List<ThoughtOnList>[4];
            for (int i = 0; i < columns.Length; i++)
                columns[i] = new List<ThoughtOnList>();
            foreach (ThoughtOnList item in ListItems())
            {
                int col = Column(item);
                if (col >= 0)
                    columns[col].Add(item);
            }

            int rows = 0;
            foreach (var column in columns)
                rows = System.Math.Max(rows, column.Count);

            var grid = new Grid(Strings.ThoughtListLabel, raggedColumns: true);
            for (int r = 0; r < rows; r++)
            {
                var row = new UIElement[columns.Length];
                for (int col = 0; col < columns.Length; col++)
                    row[col] = r < columns[col].Count
                        ? (UIElement)new ThoughtListCell(columns[col][r], _host)
                        : new GapCell();
                grid.AddRow(row);
            }
            return grid;
        }

        // The category column a thought belongs in: available (gathered, not placed), researched (placed,
        // whether cooking or fixed), forgotten, or undiscovered. A missing project sorts as undiscovered.
        private static int Column(ThoughtOnList item)
        {
            ThoughtState state = item.Project != null ? item.Project.state : ThoughtState.UNKNOWN;
            switch (state)
            {
                case ThoughtState.KNOWN: return 0;     // available
                case ThoughtState.COOKING:
                case ThoughtState.DISCOVERED:
                case ThoughtState.FIXED: return 1;     // researched (placed)
                case ThoughtState.FORGOTTEN: return 2; // forgotten
                default: return 3;                     // undiscovered
            }
        }

        private static ThoughtSlot[] Slots()
        {
            ThoughtSlotsTree tree = Object.FindObjectOfType<ThoughtSlotsTree>();
            return tree != null && tree.Slots != null ? tree.Slots : new ThoughtSlot[0];
        }

        // Every active list entry, in the game's on-screen order (its sibling order under the scroll content).
        private static List<ThoughtOnList> ListItems()
        {
            var items = new List<ThoughtOnList>();
            foreach (ThoughtOnList item in Object.FindObjectsOfType<ThoughtOnList>())
                if (item.gameObject.activeInHierarchy)
                    items.Add(item);
            items.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return items;
        }

        // Slot count and active list-entry count - the structural shape, ignoring occupancy and stage.
        private static int Signature()
        {
            int slots = Slots().Length;
            int entries = 0;
            foreach (ThoughtOnList item in Object.FindObjectsOfType<ThoughtOnList>())
                if (item.gameObject.activeInHierarchy)
                    entries++;
            return slots * 1000 + entries;
        }
    }
}
