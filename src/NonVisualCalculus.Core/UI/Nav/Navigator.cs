using System;
using System.Collections.Generic;

namespace NonVisualCalculus.Core.UI.Nav
{
    /// <summary>
    /// Owns navigation: consumes semantic UI actions, holds the focus path (within the screen, excluding
    /// the root), and centralizes focus-path diffing + announcement. Speech is injected as a delegate so
    /// Core stays engine-free and the navigator is unit-testable. Pluggable by subclass
    /// (<see cref="TraditionalNavigator"/> for the Windows-screen-reader style).
    ///
    /// Focus mutations are silent: a navigation step snapshots the path, mutates it (including any
    /// recursive auto-descend), then announces the diff ONCE.
    /// </summary>
    public abstract class Navigator
    {
        // (text, interrupt) - interrupt true supersedes current speech (a focus move); false queues.
        private readonly Action<string, bool> _speak;

        protected readonly List<UIElement> Path = new List<UIElement>();
        protected Container? Root { get; private set; }

        protected Navigator(Action<string, bool> speak) => _speak = speak;

        public UIElement? Current => Path.Count > 0 ? Path[Path.Count - 1] : null;

        /// <summary>The current focus path (root excluded), outermost first. Read-only view for dev
        /// introspection; navigation still mutates the path only through the Navigator's own methods.</summary>
        public IReadOnlyList<UIElement> FocusPath => Path;

        /// <summary>Bind to a screen root and set initial focus silently. The caller announces the screen
        /// name and then <see cref="AnnounceCurrent"/> for the landing. A null root detaches (no nav).</summary>
        public void Attach(Container? root)
        {
            Root = root;
            Path.Clear();
            if (root != null) BuildInitialFocus();
        }

        protected abstract void BuildInitialFocus();

        /// <summary>Recover focus after a dynamic rebuild: if the focused leaf was removed from the tree
        /// (its parent no longer lists it) or there is none, re-land on the root's first focusable and
        /// return true so the caller can announce the new focus. Returns false when focus is still valid
        /// (the common case - a rebuild that did not touch the focused branch).</summary>
        public bool EnsureFocusValid()
        {
            if (Root == null) return false;
            UIElement? cur = Current;
            if (cur != null && !IsOrphaned(cur)) return false;
            Path.Clear();
            BuildInitialFocus();
            return true;
        }

        // Focus is orphaned if the chain from the focused leaf up to the root is broken at ANY link, not
        // just the first: a rebuild can swap a whole nested subtree (e.g. the options screen replaces its
        // inner list), leaving the leaf's immediate parent still listing it while an ancestor higher up no
        // longer does. Walk every link to the root.
        private bool IsOrphaned(UIElement leaf)
        {
            UIElement node = leaf;
            while (node != Root)
            {
                Container? parent = node.Parent;
                if (parent == null || !parent.Contains(node)) return true;
                node = parent;
            }
            return false;
        }

        /// <summary>Handle a semantic UI action (a <see cref="UiActions"/> key). Returns true if consumed.</summary>
        public abstract bool Handle(string actionKey);

        /// <summary>Move focus to a specific element (e.g. restoring the grid position after a screen rebuilt
        /// its content under the cursor). Rebuilds the path to it and syncs the platform cursor; speaks the
        /// landing only when <paramref name="announce"/> is set (a caller that returns "refocused" to the
        /// ScreenManager leaves the announce to it, to avoid double-speaking). The landing supersedes current
        /// speech by default; pass <paramref name="interrupt"/> false to queue it when the move rides an
        /// action whose own feedback is still speaking (the pawnshop's money-gained line).</summary>
        public void Focus(UIElement target, bool announce, bool interrupt = true)
        {
            var snapshot = new List<UIElement>(Path);
            BuildPathTo(target);
            if (announce) AnnounceDelta(snapshot, interrupt);
            else Current?.OnFocused();
        }

        private static readonly List<UIElement> EmptyPath = new List<UIElement>();

        /// <summary>Announce the full focus path (screen entry): diff from empty, so container labels plus
        /// the focused leaf are read, e.g. "main menu, list, Continue, button".</summary>
        public void AnnounceCurrent() => AnnounceDelta(EmptyPath, interrupt: false);

        protected void Speak(string text, bool interrupt)
        {
            if (!string.IsNullOrEmpty(text)) _speak(text, interrupt);
        }

        /// <summary>Append an element; if it's a container, descend to its innermost remembered child
        /// (else first focusable).</summary>
        protected void AppendWithDescend(UIElement element)
        {
            Path.Add(element);
            var container = element as Container;
            while (container != null)
            {
                var next = RepresentativeChild(container);
                if (next == null) break;
                container.SetFocusedChild(next);
                Path.Add(next);
                container = next as Container;
            }
        }

        /// <summary>The child to land on when entering a container: remembered focus, else first focusable.</summary>
        protected static UIElement? RepresentativeChild(Container c)
        {
            if (c.FocusedChild != null && c.FocusedChild.CanFocus && !IsEmptyContainer(c.FocusedChild))
                return c.FocusedChild;
            return c.FirstFocusable();
        }

        private static bool IsEmptyContainer(UIElement e) => e is Container c && c.IsEmptyContainer;

        /// <summary>Rebuild the path as the ancestor chain from the root down to <paramref name="target"/>,
        /// setting each container's remembered focus along the way.</summary>
        protected void BuildPathTo(UIElement target)
        {
            Path.Clear();
            var chain = new List<UIElement>();
            var e = target;
            while (e != null && e != Root)
            {
                chain.Add(e);
                if (e.Parent != null) e.Parent.SetFocusedChild(e);
                e = e.Parent;
            }
            chain.Reverse();
            Path.AddRange(chain);
        }

        /// <summary>Diff a pre-move snapshot against the settled path and speak the delta: newly-entered
        /// nodes in path order (descend/sibling), or just the new innermost element (ascend).</summary>
        protected void AnnounceDelta(List<UIElement> oldPath, bool interrupt)
        {
            int i = 0;
            while (i < oldPath.Count && i < Path.Count && oldPath[i] == Path[i]) i++;

            if (i < Path.Count)
            {
                var parts = new List<string>();
                for (int j = i; j < Path.Count; j++)
                {
                    // Skip a container whose label just duplicates the node beneath it.
                    if (j + 1 < Path.Count)
                    {
                        var label = Path[j].Label;
                        if (!string.IsNullOrEmpty(label) && label == Path[j + 1].Label) continue;
                    }
                    var d = Path[j].GetFocusText();
                    if (!string.IsNullOrEmpty(d)) parts.Add(d);
                }
                if (parts.Count > 0) Speak(Text.SpokenLine.Join(", ", parts), interrupt);
            }
            else if (Current != null)
            {
                Speak(Current.GetFocusText(), interrupt); // ascended: announce the now-innermost focus
            }

            // Focus has settled on a new leaf (this is the one chokepoint every move and the initial
            // landing pass through): let it sync the platform cursor to our focus.
            Current?.OnFocused();
        }

        /// <summary>Ordered Tab-stops: descend through Panels; a list is one stop (its representative item).</summary>
        protected List<UIElement> ComputeTabStops()
        {
            var stops = new List<UIElement>();
            if (Root != null) AddStops(Root, stops);
            return stops;
        }

        private static void AddStops(Container c, List<UIElement> stops)
        {
            if (c.Shape != ContainerShape.Panel)
            {
                var item = RepresentativeChild(c);
                if (item != null) stops.Add(item);
                return;
            }
            foreach (var child in c.Children)
            {
                if (child is Container cc) AddStops(cc, stops);
                else if (child.CanFocus) stops.Add(child);
            }
        }
    }
}
