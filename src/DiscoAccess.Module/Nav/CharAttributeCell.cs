using DiscoAccess.Core.UI;
using DiscoAccess.Core.UI.Nav;

namespace DiscoAccess.Module.Nav
{
    /// <summary>
    /// The leftmost cell of an in-game character-sheet grid row: the row's attribute (Intellect, Psyche,
    /// Physique, Motorics), wrapping the live <see cref="StatPanel"/>. Read-only - attributes are fixed in
    /// play - so it advertises no actions; it reads the attribute's name, value, and grade live through the
    /// shared <see cref="AbilityAdapter"/> and Core <see cref="AbilityAnnouncer"/>, and follows the game's
    /// cursor on focus so its panel highlights. On focus it also clears the detail region's subject: the
    /// info panel cannot show an attribute (selecting one throws), so the detail region drops out of the Tab
    /// order rather than reading the last-focused skill.
    /// </summary>
    internal sealed class CharAttributeCell : UIElement
    {
        private readonly StatPanel _panel;
        private readonly SkillDetailCell _detail;

        public CharAttributeCell(StatPanel panel, SkillDetailCell detail)
        {
            _panel = panel;
            _detail = detail;
        }

        public override bool CanFocus => _panel != null && _panel.isActiveAndEnabled;

        public override string GetFocusText()
        {
            AbilityState s = AbilityAdapter.TryRead(_panel);
            return s != null ? AbilityAnnouncer.Compose(s) : string.Empty;
        }

        // Make this attribute the game's selection so its panel highlights, and clear the detail subject so
        // the detail region is not reachable for an attribute (which has no skill detail to show).
        public override void OnFocused()
        {
            GameCursor.Follow(_panel);
            _detail.Subject = null;
        }
    }
}
