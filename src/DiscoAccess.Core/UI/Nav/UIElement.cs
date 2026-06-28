using System.Collections.Generic;

namespace DiscoAccess.Core.UI.Nav
{
    /// <summary>
    /// A navigable element. Leaves yield the parts that compose into the spoken focus message and
    /// advertise actions (activate, back, ...); they do NOT handle keys or navigation - the Navigator
    /// does. The announcement model is intentionally simple for now (label, role, value); the reference
    /// design's typed-Announcement framework can layer in when tables/trees need richer composition.
    /// </summary>
    public abstract class UIElement
    {
        public Container? Parent { get; internal set; }

        public virtual bool CanFocus => true;

        /// <summary>The element's name/text. Read live at announce time (never cached).</summary>
        public virtual string? Label => null;

        /// <summary>A short type word spoken after the label (e.g. "button", "toggle"), or null.</summary>
        public virtual string? Role => null;

        /// <summary>The element's current value/state (e.g. "on", "50 percent"), or null.</summary>
        public virtual string? Value => null;

        /// <summary>
        /// True if activating changes this element's value in place (a toggle, a slider) so the navigator
        /// re-announces it. False for buttons that open another screen (the screen change announces itself).
        /// </summary>
        public virtual bool ReannounceOnActivate => false;

        /// <summary>The actions this element advertises. Navigators invoke them by id.</summary>
        public virtual IEnumerable<ElementAction> GetActions() { yield break; }

        /// <summary>Find an advertised action by id and run it. Returns true if found.</summary>
        public bool InvokeAction(string id)
        {
            foreach (var a in GetActions())
                if (a.Id == id) { a.Execute(); return true; }
            return false;
        }

        /// <summary>The composed spoken focus message: label, role, value, joined by ", " (non-empty only).</summary>
        public string GetFocusText()
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(Label)) parts.Add(Label!);
            if (!string.IsNullOrEmpty(Role)) parts.Add(Role!);
            if (!string.IsNullOrEmpty(Value)) parts.Add(Value!);
            return string.Join(", ", parts);
        }

        /// <summary>Just the changed state, for re-announcing after an in-place activation.</summary>
        public string GetValueText() => Value ?? "";
    }
}
