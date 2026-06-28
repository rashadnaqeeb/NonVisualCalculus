using System;
using System.Collections.Generic;

namespace DiscoAccess.Core.UI.Nav
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

        /// <summary>Bind to a screen root and set initial focus silently. The caller announces the screen
        /// name and then <see cref="AnnounceCurrent"/> for the landing. A null root detaches (no nav).</summary>
        public void Attach(Container? root)
        {
            Root = root;
            Path.Clear();
            if (root != null) BuildInitialFocus();
        }

        protected abstract void BuildInitialFocus();

        /// <summary>Handle a semantic UI action (a <see cref="UiActions"/> key). Returns true if consumed.</summary>
        public abstract bool Handle(string actionKey);

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
            if (c.FocusedChild != null && c.FocusedChild.CanFocus && !IsEmptyPanel(c.FocusedChild))
                return c.FocusedChild;
            return c.FirstFocusable();
        }

        private static bool IsEmptyPanel(UIElement e) => e is Container c && c.IsEmptyPanel;

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
                if (parts.Count > 0) Speak(string.Join(", ", parts), interrupt);
            }
            else if (Current != null)
            {
                Speak(Current.GetFocusText(), interrupt); // ascended: announce the now-innermost focus
            }
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
