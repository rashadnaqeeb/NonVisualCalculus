using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The leftmost cell of a signature skill grid row: the row's attribute (Intellect, Psyche, Physique,
    /// Motorics), wrapping the live <see cref="StatPanel"/>. Read-only here - the abilities are fixed by the
    /// previous step - so it advertises no actions; it reads the attribute name, value, grade, and
    /// description live through the shared <see cref="AbilityAdapter"/> and Core <see cref="AbilityAnnouncer"/>,
    /// the same way the Adjust Abilities screen does, and follows the game's cursor on focus so its panel
    /// highlights.
    /// </summary>
    internal sealed class AttributeCell : UIElement
    {
        private readonly StatPanel _panel;

        public AttributeCell(StatPanel panel) => _panel = panel;

        public override bool CanFocus => _panel != null && _panel.isActiveAndEnabled;

        public override string GetFocusText()
        {
            AbilityState s = AbilityAdapter.TryRead(_panel);
            return s != null ? AbilityAnnouncer.Compose(s) : string.Empty;
        }

        // Make this attribute the game's selection as focus lands, so its panel highlights.
        public override void OnFocused() => GameCursor.Follow(_panel);
    }
}
