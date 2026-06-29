using System.Collections.Generic;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// A structural blueprint: holds children, remembers its focused child (for restore on re-entry), and
    /// exposes shape geometry. Navigation policy lives in the Navigator. A labeled container announces just
    /// its label when focus enters it; an unlabeled panel is silent structure. Per the house rule, no
    /// positional counts ("3 of 10") are announced - the user tracks position.
    /// </summary>
    public class Container : UIElement
    {
        private readonly List<UIElement> _children = new List<UIElement>();
        private readonly string? _label;

        public IReadOnlyList<UIElement> Children => _children;

        /// <summary>Whether <paramref name="element"/> is a current child (false once a rebuild has cleared
        /// it, even though its Parent back-pointer still points here).</summary>
        public bool Contains(UIElement element) => _children.Contains(element);

        /// <summary>Remembered focus within this container, for restore on re-entry.</summary>
        public UIElement? FocusedChild { get; private set; }

        public ContainerShape Shape { get; protected set; }

        public Container(ContainerShape shape = ContainerShape.VerticalList, string? label = null)
        {
            Shape = shape;
            _label = label;
        }

        public override string? Label => _label;

        // A container speaks only its label on entry, never a role word; a panel is pure structure (no
        // label). The container's identity is its label, so a role suffix ("list") was just noise.
        public override string? Role => null;

        /// <summary>A container with nothing focusable inside - never a landing target. Covers a structural
        /// Panel (pure layout) and a content list/table/grid that is momentarily empty (e.g. a grid built a
        /// frame before its cells exist, repopulated on the next update). The navigator's descent and the
        /// first/last-focusable scans all skip it; centralized here so the "skippable empty container" rule
        /// lives in one place.</summary>
        public bool IsEmptyContainer => FirstFocusable() == null;

        public void Add(UIElement element)
        {
            element.Parent = this;
            _children.Add(element);
        }

        public virtual void Clear()
        {
            _children.Clear();
            FocusedChild = null;
        }

        public void SetFocusedChild(UIElement? element) => FocusedChild = element;

        /// <summary>First child the navigator may land on (skips non-focusable, and containers with nothing
        /// focusable inside - descending into an empty one would strand focus on silent structure).</summary>
        public UIElement? FirstFocusable()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (!_children[i].CanFocus) continue;
                if (_children[i] is Container c && c.IsEmptyContainer) continue;
                return _children[i];
            }
            return null;
        }

        /// <summary>Last child the navigator may land on (the End-jump target); mirrors <see cref="FirstFocusable"/>.</summary>
        public UIElement? LastFocusable()
        {
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (!_children[i].CanFocus) continue;
                if (_children[i] is Container c && c.IsEmptyContainer) continue;
                return _children[i];
            }
            return null;
        }

        /// <summary>Next focusable child from <paramref name="from"/> in a direction (list shapes only).</summary>
        public UIElement? GetNeighbor(UIElement from, NavDirection dir)
        {
            int step = StepFor(dir);
            if (step == 0) return null;
            int idx = _children.IndexOf(from);
            if (idx < 0) return null;
            for (int i = idx + step; i >= 0 && i < _children.Count; i += step)
            {
                if (!_children[i].CanFocus) continue;
                if (_children[i] is Container c && c.IsEmptyContainer) continue;
                return _children[i];
            }
            return null;
        }

        private int StepFor(NavDirection dir)
        {
            if (Shape == ContainerShape.VerticalList)
                return dir == NavDirection.Down ? 1 : dir == NavDirection.Up ? -1 : 0;
            if (Shape == ContainerShape.HorizontalList)
                return dir == NavDirection.Right ? 1 : dir == NavDirection.Left ? -1 : 0;
            return 0; // Panel uses Tab traversal.
        }
    }
}
