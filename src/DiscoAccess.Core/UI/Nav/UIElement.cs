using System.Collections.Generic;
using static DiscoAccess.Core.Strings.Strings;

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

        /// <summary>The composed spoken focus message: label, role, value, joined by ", " (non-empty only,
        /// each part un-RTL-fixed - the label is game text that can arrive display-shaped). Virtual so an
        /// element with a richer composition (e.g. an options control that also speaks its type and
        /// tooltip via a Core composer) can override the default label/role/value join.</summary>
        public virtual string GetFocusText()
            => Text.SpokenLine.Join(Label, Role, Value);

        /// <summary>Just the changed state, for re-announcing after an in-place activation.</summary>
        public string GetValueText() => Value ?? "";

        /// <summary>What to announce after an in-place adjust action (increase/decrease) just ran;
        /// <paramref name="changed"/> is whether the value text actually moved. A move reads the new
        /// value; an adjust that moved nothing hit a bound, so the default names it ("minimum" /
        /// "maximum") rather than read the same value back. An element overrides this for a control whose
        /// ends are not a magnitude (an option dropdown re-reads its choice) or that has a richer reason
        /// for not moving (an ability raise rejected because no points remain).</summary>
        public virtual string GetAdjustText(string actionId, bool changed)
        {
            if (changed)
                return GetValueText();
            return actionId == ActionIds.Increase ? StatusMaximum : StatusMinimum;
        }

        /// <summary>Called by the navigator when this element becomes the focused leaf after a move. The
        /// default does nothing; an engine-coupled element overrides it to sync the platform's own cursor
        /// to our focus, so the game's selection follows ours.</summary>
        public virtual void OnFocused() { }
    }
}
