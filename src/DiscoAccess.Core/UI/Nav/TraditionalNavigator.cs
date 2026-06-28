using System;
using System.Collections.Generic;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// Windows-screen-reader-style navigation: Tab / Shift-Tab traverse Panel tab-stops (a list counts as
    /// one stop), arrows move within a list (or adjust a focused slider/stepper), Enter activates, Escape
    /// asks the screen to go back, Home/End jump to a list's ends. Entering a container auto-focuses its
    /// representative child.
    ///
    /// It also drives type-ahead search over the focused list/grid: the module's raw-key reader feeds
    /// typed letters in via <see cref="TypeSearchChar"/> (it owns the engine-coupled key reading; this
    /// side owns the matching engine, focus landing, and announcement, all unit-testable). While a search
    /// is live, Up/Down step the matches, Home/End jump to the first/last, Escape clears, and any other
    /// nav key ends the search and acts normally - all handled in <see cref="Handle"/>.
    /// </summary>
    public sealed class TraditionalNavigator : Navigator
    {
        private readonly TypeAheadSearch _search = new TypeAheadSearch();
        private readonly List<UIElement> _searchItems = new List<UIElement>();
        // Where the search last landed focus, so a focus move under us (a screen rebuild re-homing focus)
        // is detected and the now-stale search dropped. Null when no search is live.
        private UIElement? _searchFocus;

        public TraditionalNavigator(Action<string, bool> speak) : base(speak)
        {
            _search.OnNoMatch = buffer => Speak(SearchNoMatch(buffer), interrupt: true);
        }

        protected override void BuildInitialFocus()
        {
            ClearSearch(announce: false); // a (re)attached or re-homed screen starts with no live search
            if (Root == null) return;
            var first = RepresentativeChild(Root);
            if (first == null) return;
            Root.SetFocusedChild(first);
            AppendWithDescend(first);
        }

        /// <summary>Whether a type-ahead search is currently live. Exposed for callers reasoning about
        /// search state (and the tests); the module's per-frame reader gates on the text-edit state and
        /// <see cref="SearchHasBuffer"/> instead, not this.</summary>
        public bool SearchActive => _search.IsSearchActive;

        /// <summary>Whether the search buffer holds at least one character. The module's reader gates a
        /// typed Space on this so a lone Space (no buffer) is not swallowed into an empty search.</summary>
        public bool SearchHasBuffer => _search.HasBuffer;

        /// <summary>Feed one typed character into the search: (re)scope to the focused list/grid, append,
        /// match, and land on the best result (announced interrupting, since typing is rapid). A focus move
        /// since the last result starts a fresh search at the new location. Called by the module's raw-key
        /// reader, never from the action dispatch.</summary>
        public void TypeSearchChar(char c)
        {
            if (_searchFocus != null && Current != _searchFocus)
                ClearSearch(announce: false); // focus moved under us -> start fresh here
            _search.AddChar(c);
            RunSearch();
        }

        /// <summary>Delete the last typed character and re-match. With nothing to delete it does nothing;
        /// deleting the last character clears the search (announced, like Escape). Called by the module's
        /// raw-key reader on Backspace.</summary>
        public void BackspaceSearch()
        {
            if (!_search.HasBuffer) return; // nothing typed -> Backspace is not ours
            if (_searchFocus != null && Current != _searchFocus)
            {
                ClearSearch(announce: false); // focus moved under us -> the buffer is stale, drop it
                return;
            }
            _search.RemoveChar();
            if (!_search.HasBuffer) { ClearSearch(announce: true); return; } // emptied -> end the search
            RunSearch();
        }

        // Re-scope to the live focused list/grid and run the current buffer against it, landing on the best
        // result. Shared by typing and backspacing; the UI is live, so the scope is rebuilt each time.
        private void RunSearch()
        {
            RebuildSearchScope();
            if (_searchItems.Count == 0) return;
            _search.Search(_searchItems.Count, i => SearchName(_searchItems[i]), SearchFocusResult);
        }

        // What an item is matched by: a grid cell matches on its ROW text (the save line) so a whole row is
        // one result, not one per action column; everything else matches on its full focus text (label,
        // role, value), where the leading label is what the user is typing toward.
        private static string SearchName(UIElement item)
            => item is GridCell gc && !string.IsNullOrEmpty(gc.RowText) ? gc.RowText! : item.GetFocusText();

        public override bool Handle(string actionKey)
        {
            // A live search reserves the result-stepping keys: Up/Down walk the matches, Home/End jump to
            // the first/last, Escape clears it. (Letters and Space feed the buffer from the module's raw
            // reader, not here.) Any other key ends the search and acts normally - Enter activates the
            // found item, Tab leaves, Left/Right adjust or move. If focus has moved out from under the
            // search since it last landed (a screen rebuild), the results are stale: drop them silently and
            // treat this key as a normal one.
            if (_search.IsSearchActive)
            {
                if (_searchFocus != null && Current != _searchFocus)
                    ClearSearch(announce: false);
                else
                    switch (actionKey)
                    {
                        case UiActions.Up: _search.NavigateResults(-1); return true;
                        case UiActions.Down: _search.NavigateResults(1); return true;
                        case UiActions.Home: _search.JumpToFirstResult(); return true;
                        case UiActions.End: _search.JumpToLastResult(); return true;
                        case UiActions.Back: ClearSearch(announce: true); return true;
                        default: ClearSearch(announce: false); break; // fall through to normal handling
                    }
            }

            switch (actionKey)
            {
                case UiActions.Up: return Arrow(NavDirection.Up);
                case UiActions.Down: return Arrow(NavDirection.Down);
                case UiActions.Left: return Arrow(NavDirection.Left);
                case UiActions.Right: return Arrow(NavDirection.Right);
                case UiActions.Next: return Tab(1);
                case UiActions.Prev: return Tab(-1);
                case UiActions.Home: return JumpEdge(first: true);
                case UiActions.End: return JumpEdge(first: false);
                case UiActions.Activate:
                {
                    if (Current == null) return false;
                    // Consume only when something actually activated; a focused element with no Activate
                    // action leaves the key unconsumed rather than silently eating it.
                    bool activated = Current.InvokeAction(ActionIds.Activate);
                    if (activated && Current.ReannounceOnActivate)
                        Speak(Current.GetValueText(), interrupt: true);
                    return activated;
                }
                case UiActions.Back:
                    // Screen-level back/close: consume only if the root advertises a back action.
                    return Root != null && Root.InvokeAction(ActionIds.Back);
                default:
                    return false; // not a nav key
            }
        }

        private bool Arrow(NavDirection dir)
        {
            if (Current == null) return false;

            // A focused slider/stepper advertises increase/decrease; Left/Right adjust it and re-announce
            // just the new value.
            if (dir == NavDirection.Left && Current.InvokeAction(ActionIds.Decrease))
            { Speak(Current.GetValueText(), interrupt: true); return true; }
            if (dir == NavDirection.Right && Current.InvokeAction(ActionIds.Increase))
            { Speak(Current.GetValueText(), interrupt: true); return true; }

            // A grid moves a 2-D cell cursor and announces only the axis that changed.
            if (Current.Parent is Grid grid) return GridArrow(dir, grid);

            var snapshot = new List<UIElement>(Path);
            if (!Move(dir)) return false;
            AnnounceDelta(snapshot, interrupt: true);
            return true;
        }

        // Move the cell cursor in a grid and announce Excel-style: the column header when the column
        // changed, the row text when the row changed, then the cell's own value if any. A non-focusable
        // cell (e.g. a non-deletable save's Delete) is scanned past in the move direction; running off the
        // grid consumes the key without wrapping.
        private bool GridArrow(NavDirection dir, Grid grid)
        {
            if (!grid.TryCoords(Current!, out int r, out int c)) return false;
            int nr = r, nc = c;
            UIElement? next = null;
            while (true)
            {
                switch (dir)
                {
                    case NavDirection.Down: nr++; break;
                    case NavDirection.Up: nr--; break;
                    case NavDirection.Right: nc++; break;
                    case NavDirection.Left: nc--; break;
                }
                var cell = grid.CellAt(nr, nc);
                if (cell == null) return true; // off the edge: consume, no wrap
                if (cell.CanFocus) { next = cell; break; }
            }

            LandOnCell(next, announceColumn: nc != c, announceRow: nr != r);
            return true;
        }

        // Move focus onto a grid cell and announce Excel-style: only the axis text the caller says changed
        // (the column header on a column move, the row text on a row move), falling back to the cell's full
        // focus text if neither axis has anything to say. Then sync the platform cursor. Shared by the grid
        // arrow and Home/End jumps so they land and read consistently.
        private void LandOnCell(UIElement cell, bool announceColumn, bool announceRow)
        {
            BuildPathTo(cell);
            var parts = new List<string>(2);
            if (cell is GridCell gc)
            {
                if (announceColumn && !string.IsNullOrEmpty(gc.ColumnHeader)) parts.Add(gc.ColumnHeader!);
                if (announceRow && !string.IsNullOrEmpty(gc.RowText)) parts.Add(gc.RowText!);
            }
            if (parts.Count == 0)
            {
                var text = cell.GetFocusText();
                if (!string.IsNullOrEmpty(text)) parts.Add(text);
            }
            if (parts.Count > 0) Speak(string.Join(", ", parts), interrupt: true);
            cell.OnFocused();
        }

        // Arrow movement within list-shaped containers, spilling into a same-shape parent at the edge.
        private bool Move(NavDirection dir)
        {
            var movingFrom = Current;
            var container = movingFrom?.Parent;
            while (container != null && movingFrom != null)
            {
                var next = container.GetNeighbor(movingFrom, dir);
                if (next != null)
                {
                    int idx = Path.IndexOf(movingFrom);
                    if (idx >= 0) Path.RemoveRange(idx, Path.Count - idx);
                    AppendWithDescend(next);
                    container.SetFocusedChild(next);
                    return true;
                }
                var parent = container.Parent;
                if (parent != null && parent.Shape == container.Shape)
                {
                    movingFrom = container;
                    container = parent;
                    continue;
                }
                return false;
            }
            return false;
        }

        private bool Tab(int step)
        {
            var stops = ComputeTabStops();
            if (stops.Count == 0) return false;

            // Current may be deeper than its tab-stop (an item inside a list whose stop is the list's
            // representative), so walk up to the nearest element that IS a stop.
            int idx = -1;
            for (var e = Current; e != null && idx < 0; e = e.Parent)
                idx = stops.IndexOf(e);

            int ni = idx < 0 ? (step >= 0 ? 0 : stops.Count - 1) : idx + step;
            if (ni < 0 || ni >= stops.Count) return true; // at an end; consume, no wrap

            var snapshot = new List<UIElement>(Path);
            BuildPathTo(stops[ni]);
            // Re-descend so re-entering a list restores its remembered/representative item.
            var landed = Current;
            if (landed != null) { Path.RemoveAt(Path.Count - 1); AppendWithDescend(landed); }
            AnnounceDelta(snapshot, interrupt: true);
            return true;
        }

        // Home/End: jump to the first/last item of the list the focus is in.
        private bool JumpEdge(bool first)
        {
            var container = Current?.Parent;

            // In a grid, jump to the first/last focusable cell of the current column (only the row
            // changes, so just the row text is announced).
            if (container is Grid grid)
            {
                if (!grid.TryCoords(Current!, out _, out int col)) return true;
                int step = first ? 1 : -1;
                int start = first ? 0 : grid.RowCount - 1;
                UIElement? edge = null;
                for (int rr = start; rr >= 0 && rr < grid.RowCount; rr += step)
                {
                    var cell = grid.CellAt(rr, col);
                    if (cell != null && cell.CanFocus) { edge = cell; break; }
                }
                if (edge == null || edge == Current) return true;
                // A Home/End jump stays in the column, so only the row changed: announce just the row text.
                LandOnCell(edge, announceColumn: false, announceRow: true);
                return true;
            }

            if (container == null
                || (container.Shape != ContainerShape.VerticalList && container.Shape != ContainerShape.HorizontalList))
                return true; // focused but in no jumpable list - consume, do nothing

            var target = first ? container.FirstFocusable() : container.LastFocusable();
            if (target == null || target == Current) return true;

            var snapshot = new List<UIElement>(Path);
            int idx = Path.IndexOf(Current!);
            if (idx >= 0) Path.RemoveRange(idx, Path.Count - idx);
            AppendWithDescend(target);
            container.SetFocusedChild(target);
            AnnounceDelta(snapshot, interrupt: true);
            return true;
        }

        // ---- Type-ahead search (engine in TypeAheadSearch; this is the glue) ----

        // The searchable scope = the list/grid the focus lives in: climb from the focus to the outermost
        // enclosing container, stopping at a Panel boundary (the tab-stop divider) or at the top. The root
        // may itself BE the searchable list/grid - the title menu's root is a bare list with no Panel
        // wrapper - so the climb must not stop short at the root; only a Panel ends it.
        private void RebuildSearchScope()
        {
            _searchItems.Clear();
            UIElement? scope = Current;
            if (scope == null) return;
            while (scope.Parent != null && scope.Parent.Shape != ContainerShape.Panel)
                scope = scope.Parent;

            if (scope is Grid grid)
            {
                // A grid searches by ROW (e.g. a save line), not by cell: one representative cell per row,
                // taken at the focused column where that row has one, so a match keeps the column and
                // Left/Right still move between that row's action cells. Matching is on the row text - see
                // SearchName - so a row is a single result regardless of how many action columns it has.
                int col = grid.TryCoords(Current!, out _, out int c) ? c : 0;
                for (int r = 0; r < grid.RowCount; r++)
                {
                    UIElement? rep = RowRepresentative(grid, r, col);
                    if (rep != null) _searchItems.Add(rep);
                }
            }
            else if (scope is Container cont) CollectSearchLeaves(cont, _searchItems);
            else if (scope.CanFocus) _searchItems.Add(scope);
        }

        // The cell a grid row contributes to a search: the cell at the preferred column if focusable, else
        // the row's first focusable cell. Landing on it keeps the focused column when the row has one (a
        // non-deletable save searched from the Delete column lands on its Load/Save action instead).
        private static UIElement? RowRepresentative(Grid grid, int row, int preferCol)
        {
            UIElement? pref = grid.CellAt(row, preferCol);
            if (pref != null && pref.CanFocus) return pref;
            for (int c = 0; c < grid.ColCount; c++)
            {
                UIElement? cell = grid.CellAt(row, c);
                if (cell != null && cell.CanFocus) return cell;
            }
            return null;
        }

        private static void CollectSearchLeaves(Container c, List<UIElement> outList)
        {
            foreach (var child in c.Children)
            {
                if (child is Container cc) CollectSearchLeaves(cc, outList);
                else if (child.CanFocus) outList.Add(child);
            }
        }

        // Land on a search result: a real focus move (path + remembered focus), announced interrupting -
        // typing is rapid, each refinement should cut off the last. The index is into _searchItems.
        private void SearchFocusResult(int index)
        {
            if (index < 0 || index >= _searchItems.Count) return;
            var target = _searchItems[index];
            var snapshot = new List<UIElement>(Path);
            BuildPathTo(target);
            _searchFocus = Current;
            AnnounceDelta(snapshot, interrupt: true);
        }

        /// <summary>Drop any live search. <paramref name="announce"/> speaks "search cleared" (the explicit
        /// Escape case); a silent clear is used on every other teardown (focus moved, screen re-homed, a
        /// non-search key pressed).</summary>
        public void ClearSearch(bool announce)
        {
            bool had = _search.IsSearchActive || _search.HasBuffer;
            _search.Clear();
            _searchItems.Clear();
            _searchFocus = null;
            if (announce && had) Speak(SearchCleared, interrupt: true);
        }
    }
}
