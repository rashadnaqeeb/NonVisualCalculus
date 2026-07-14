using System;
using System.Collections.Generic;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.Strings;
using NonVisualCalculus.Core.UI.Nav;
using NonVisualCalculus.Core.World;
using NonVisualCalculus.Module.Input;
using NonVisualCalculus.Module.World;
using Snv = System.Numerics.Vector3;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The mod's bookmarks menu (Ctrl+B): the current map's saved positions as a table sorted nearest
    /// the character first - one row per bookmark, a walk column then a delete column (the save
    /// screen's shape) - with the add-bookmark row last (End jumps to it). A row speaks its name,
    /// distance, and whether a path connects, so the player hears "can't reach" before committing;
    /// activating walk closes the menu and drives the game's own party move. Bookmarks live in the
    /// <see cref="BookmarkStore"/> file scoped by scene; the list is re-read from it and the live game
    /// on every open and after every change, never held.
    /// </summary>
    internal sealed class BookmarksScreen : ModOverlay
    {
        private readonly BookmarkStore _store;
        private readonly WorldReader _world;
        private IModHost _host;
        private Table _table;
        private Action _close;
        // The stored list changed under the table (an add or delete); OnUpdate rebuilds and re-homes.
        private bool _dirty;

        public BookmarksScreen(BookmarkStore store, WorldReader world)
        {
            _store = store;
            _world = world;
        }

        public override string Title => Strings.ScreenBookmarks;

        public override Container BuildRoot(IModHost host, Action onClose)
        {
            _host = host;
            _close = onClose;
            var root = new OverlayRoot(onClose);
            _table = new Table();
            Populate();
            root.Add(_table);
            return root;
        }

        public override bool OnUpdate(IModHost host, TraditionalNavigator nav)
        {
            if (!_dirty) return false;
            _dirty = false;
            // Keep the player's place through the rebuild: same column, nearest surviving row (the
            // save screen's re-home rule).
            int row = 0, col = 0;
            bool hadCoords = nav.Current != null && _table.TryCoords(nav.Current, out row, out col);
            Populate();
            if (hadCoords)
            {
                UIElement target = NearestFocusable(row, col);
                if (target != null) _table.SetFocusedChild(target);
            }
            return nav.EnsureFocusValid();
        }

        // One row per bookmark on the current map, nearest first; the add row last.
        private void Populate()
        {
            _table.Clear();
            Snv player = _world.PlayerPosition;
            List<Bookmark> marks = _store.ForScene(_world.CurrentScene);
            marks.Sort((a, b) => Geo.Distance(a.Position, player).CompareTo(Geo.Distance(b.Position, player)));
            foreach (Bookmark mark in marks)
                _table.AddRow(new BookmarkWalkCell(this, mark), new BookmarkDeleteCell(this, mark));
            _table.AddRow(new BookmarkAddCell(this, _host));
        }

        // The focusable cell to land on after a rebuild, nearest the pre-rebuild position (the save
        // screen's NearestFocusable, over this table).
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
            return null;
        }

        // ---- what the cells read and do (live reads; the screen holds no copy of game state) ----

        /// <summary>A bookmark row's spoken line: name, walking distance from the character, and
        /// "can't reach" when no navmesh path connects - all read live at announce time.</summary>
        internal string RowLine(Bookmark mark)
        {
            int meters = (int)Math.Round(Geo.Distance(mark.Position, _world.PlayerPosition));
            return BookmarkAnnouncer.Compose(mark.Name, meters, _world.CanReach(mark.Position));
        }

        /// <summary>Walk the character to a bookmark: close the menu (back to the world) and drive the
        /// game's own party move, which speaks its own feedback (moving / can't reach / orb hold).</summary>
        internal void Walk(Bookmark mark)
        {
            _close();
            _world.WalkTo(mark.Position);
        }

        internal void Delete(Bookmark mark)
        {
            if (_store.Remove(mark))
                _host.Speech.Speak(Strings.BookmarkDeleted(mark.Name), interrupt: true);
            else
                _host.Speech.Speak(Strings.BookmarkWriteFailed, interrupt: true);
            _dirty = true; // rebuild either way: the file is the truth, re-read it
        }

        /// <summary>Create a bookmark named <paramref name="name"/> at the character's position on the
        /// current map (both read live at commit time, not menu-open time).</summary>
        internal void Add(string name)
        {
            var mark = new Bookmark(_world.CurrentScene, name, _world.PlayerPosition);
            if (_store.Add(mark))
                _host.Speech.Speak(Strings.BookmarkSaved(mark.Name), interrupt: true);
            else
                _host.Speech.Speak(Strings.BookmarkWriteFailed, interrupt: true);
            _dirty = true;
        }

        /// <summary>The suggested name for a new bookmark: the first "bookmark n" the current map does
        /// not already use.</summary>
        internal string DefaultName()
        {
            List<Bookmark> marks = _store.ForScene(_world.CurrentScene);
            for (int n = marks.Count + 1; ; n++)
            {
                string candidate = Strings.BookmarkDefaultName(n);
                if (!marks.Exists(m => m.Name == candidate)) return candidate;
            }
        }
    }

    /// <summary>A bookmark row's walk column: the row is the bookmark (name, distance, reachability,
    /// read live), the column the action. Activating walks the character there and closes the menu.</summary>
    internal class BookmarkWalkCell : TableCell
    {
        protected readonly BookmarksScreen Screen;
        protected readonly Bookmark Mark;

        public BookmarkWalkCell(BookmarksScreen screen, Bookmark mark)
        {
            Screen = screen;
            Mark = mark;
        }

        public override string ColumnHeader => Strings.ActionWalk;
        public override string RowText => Screen.RowLine(Mark);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, () => Screen.Walk(Mark));
        }
    }

    /// <summary>The delete column of a bookmark row.</summary>
    internal sealed class BookmarkDeleteCell : BookmarkWalkCell
    {
        public BookmarkDeleteCell(BookmarksScreen screen, Bookmark mark) : base(screen, mark) { }

        public override string ColumnHeader => Strings.ActionDelete;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, () => Screen.Delete(Mark));
        }
    }

    /// <summary>
    /// The add-bookmark row (always last): activating starts a mod-owned name edit
    /// (<see cref="ModTextEntry"/>) - typed keys build the name, Enter saves, Escape backs out. The
    /// suggested name is spoken rather than prefilled: a bare Enter takes it, and typing starts clean
    /// instead of appending to a suggestion the player would first have to erase.
    /// </summary>
    internal sealed class BookmarkAddCell : TableCell, IModTextSession
    {
        private readonly BookmarksScreen _screen;
        private readonly IModHost _host;
        private string _buffer = "";
        private bool _editing;

        public BookmarkAddCell(BookmarksScreen screen, IModHost host)
        {
            _screen = screen;
            _host = host;
        }

        public override string ColumnHeader => null;
        public override string RowText => Strings.BookmarkAdd;
        public override string Value => _editing ? _buffer : null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Activate, BeginEdit);
        }

        private void BeginEdit()
        {
            _editing = true;
            _buffer = "";
            ModTextEntry.Active = this;
            _host.Speech.Speak(Strings.StatusEditMode, interrupt: true);
            _host.Speech.Speak(_screen.DefaultName(), interrupt: false); // what a bare Enter will name it
        }

        public void Type(char c)
        {
            if (_editing) _buffer += c;
        }

        public void Backspace()
        {
            if (_editing && _buffer.Length > 0) _buffer = _buffer.Substring(0, _buffer.Length - 1);
        }

        public void Commit()
        {
            if (!_editing) return;
            string name = BookmarkFile.Clean(_buffer);
            EndEdit();
            _screen.Add(name.Length > 0 ? name : _screen.DefaultName());
        }

        public void Cancel()
        {
            if (!_editing) return;
            EndEdit();
            _host.Speech.Speak(GetFocusText(), interrupt: true); // backed out: re-read the row
        }

        private void EndEdit()
        {
            _editing = false;
            _buffer = "";
            if (ModTextEntry.Active == this) ModTextEntry.Active = null;
        }
    }
}
