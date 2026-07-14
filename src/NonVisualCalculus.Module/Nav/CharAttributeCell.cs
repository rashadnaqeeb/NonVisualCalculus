using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// The leftmost cell of an in-game character-sheet grid row: the row's attribute (Intellect, Psyche,
    /// Physique, Motorics), wrapping the live <see cref="StatPanel"/>. Read-only - attributes are fixed in
    /// play - so it advertises no actions; it reads the attribute's name, value, and grade live through the
    /// shared <see cref="AbilityAdapter"/> and Core <see cref="AbilityAnnouncer"/>, and follows the game's
    /// cursor on focus so its panel highlights.
    /// </summary>
    internal sealed class CharAttributeCell : UIElement
    {
        private readonly StatPanel _panel;

        public CharAttributeCell(StatPanel panel)
        {
            _panel = panel;
        }

        public override bool CanFocus => _panel != null && _panel.isActiveAndEnabled;

        public override string GetFocusText()
        {
            AbilityState s = AbilityAdapter.TryRead(_panel);
            return s != null ? AbilityAnnouncer.Compose(s) : string.Empty;
        }

        // Make this attribute the game's selection so its panel highlights.
        public override void OnFocused()
        {
            GameCursor.Follow(_panel);
        }
    }
}
