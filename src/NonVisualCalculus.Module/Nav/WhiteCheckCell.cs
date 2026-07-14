using NonVisualCalculus.Core.UI;
using NonVisualCalculus.Core.UI.Nav;

namespace NonVisualCalculus.Module.Nav
{
    /// <summary>
    /// One found white check in the journal's map tab, wrapping a live <see cref="JournalWhiteCheckUI"/>
    /// (itself the game's selectable for the row). Reads the thing the check is on, the skill, its difficulty
    /// and whether it can be tried now, live at announce time through <see cref="JournalAdapter"/> and the
    /// Core <see cref="JournalAnnouncer"/>. Informational only - a check is attempted out in the world, not
    /// here - so it advertises no action; on focus it makes the row the game's selection.
    /// </summary>
    internal sealed class WhiteCheckCell : UIElement
    {
        private readonly JournalWhiteCheckUI _ui;

        public WhiteCheckCell(JournalWhiteCheckUI ui) => _ui = ui;

        public override bool CanFocus => _ui != null && _ui.isActiveAndEnabled && _ui.whiteCheck != null;

        public override string GetFocusText()
            => JournalAnnouncer.ComposeWhiteCheck(JournalAdapter.ReadWhiteCheck(_ui));

        public override void OnFocused() => GameCursor.Follow(_ui);
    }
}
