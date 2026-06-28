using System.Collections.Generic;
using static DiscoAccess.Core.Strings.Strings;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// A structural blueprint: holds children, remembers its focused child (for restore on re-entry), and
    /// exposes shape geometry. Navigation policy lives in the Navigator. A labeled list announces its
    /// label and a "list" role when focus enters it; an unlabeled panel is silent structure. Per the
    /// house rule, no positional counts ("3 of 10") are announced - the user tracks position.
    /// </summary>
    public class Container : UIElement
    {
        private readonly List<UIElement> _children = new List<UIElement>();
        private readonly string? _label;

        public IReadOnlyList<UIElement> Children => _children;

        /// <summary>Remembered focus within this container, for restore on re-entry.</summary>
        public UIElement? FocusedChild { get; private set; }

        public ContainerShape Shape { get; protected set; }

        public Container(ContainerShape shape = ContainerShape.VerticalList, string? label = null)
        {
            Shape = shape;
            _label = label;
        }

        public override string? Label => _label;

        // A labeled list reads "label, list" on entry; a panel is pure structure (no role). An unlabeled
        // container stays silent (GetFocusText drops a role with no label only via this guard).
        public override string? Role
            => _label != null && Shape != ContainerShape.Panel ? RoleList : null;

        /// <summary>A Panel with nothing focusable inside - structural only, never a landing target. The
        /// navigator's descent and the first/last-focusable scans all skip it; centralized here so the
        /// "skippable empty structure" rule lives in one place.</summary>
        public bool IsEmptyPanel => Shape == ContainerShape.Panel && FirstFocusable() == null;

        public void Add(UIElement element)
        {
            element.Parent = this;
            _children.Add(element);
        }

        public void Clear()
        {
            _children.Clear();
            FocusedChild = null;
        }

        public void SetFocusedChild(UIElement? element) => FocusedChild = element;

        /// <summary>First child the navigator may land on (skips non-focusable, and panels with nothing
        /// focusable inside - descending into an empty one would strand focus on silent structure).</summary>
        public UIElement? FirstFocusable()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (!_children[i].CanFocus) continue;
                if (_children[i] is Container c && c.IsEmptyPanel) continue;
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
                if (_children[i] is Container c && c.IsEmptyPanel) continue;
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
                if (_children[i].CanFocus) return _children[i];
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
